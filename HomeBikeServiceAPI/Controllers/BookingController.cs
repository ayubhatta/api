using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using HomeBikeServiceAPI.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.IdentityModel.Tokens;
using HomeBikeServiceAPI.Data;
using HomeBikeServiceAPI.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace HomeBikeServiceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BookingController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<BookingController> _logger;
        private readonly IBookingRepo _repo;

        public BookingController(AppDbContext context, ILogger<BookingController> logger, IBookingRepo bookingRepo)
        {
            _context = context;
            _logger = logger;
            _repo = bookingRepo;
        }


        //[Authorize(Roles = "User")]
        [HttpPost("add")]
        public async Task<IActionResult> AddToBooking(BookingDto bookingDto)
        {
            try
            {
                // Check if all required fields are provided
                if (string.IsNullOrEmpty(bookingDto.BikeChasisNumber) ||
                    string.IsNullOrEmpty(bookingDto.BikeDescription) ||
                    string.IsNullOrEmpty(bookingDto.BookingDate) ||
                    string.IsNullOrEmpty(bookingDto.BookingTime) ||
                    bookingDto.Total == null ||
                    string.IsNullOrEmpty(bookingDto.BikeNumber) ||
                    string.IsNullOrEmpty(bookingDto.BookingAddress))
                {
                    return Ok(new { success = false, message = "Please enter all fields." });
                }

                // Check if UserId exists
                var userExists = await _context.Users.AnyAsync(u => u.Id == bookingDto.UserId);
                if (!userExists)
                {
                    return Ok(new { success = false, message = "User not found." });
                }

                // Check if BikeId exists
                var bikeNumberExists = await _context.Bookings
                    .AnyAsync(b => b.BikeNumber == bookingDto.BikeNumber && b.UserId != bookingDto.UserId);
                if (bikeNumberExists)
                {
                    return Ok(new { success = false, message = "Bike number already booked." });
                }

                // Booking date and time validation
                if (!DateOnly.TryParse(bookingDto.BookingDate, out DateOnly bookingDate))
                {
                    return Ok(new { success = false, message = "Invalid Booking Date format. Please use yyyy-MM-dd." });
                }

                if (!TimeOnly.TryParse(bookingDto.BookingTime, out TimeOnly bookingTime))
                {
                    return Ok(new { success = false, message = "Invalid Booking Time format. Please use HH:mm." });
                }

                var bookingDateTime = new DateTime(bookingDate.Year, bookingDate.Month, bookingDate.Day, bookingTime.Hour, bookingTime.Minute, 0);

                if (bookingDateTime < DateTime.Now)
                {
                    return Ok(new { success = false, message = "Booking time cannot be in the past." });
                }

                var bookingStartTime = bookingDateTime.AddHours(-2);
                var bookingEndTime = bookingDateTime.AddHours(2);

                var bookingStartTimeSpan = bookingStartTime.TimeOfDay;
                var bookingEndTimeSpan = bookingEndTime.TimeOfDay;

                // Get the total number of available mechanics
                var totalMechanics = await _context.Mechanics.CountAsync();

                // Check how many bookings already exist for the same time and date
                var existingBookingsCount = _context.Bookings
                    .AsEnumerable()
                    .Where(b => b.BookingDate == bookingDate &&
                                b.BookingTime.HasValue &&
                                b.BookingTime.Value.ToTimeSpan() >= bookingStartTimeSpan &&
                                b.BookingTime.Value.ToTimeSpan() <= bookingEndTimeSpan)
                    .Count();

                // Ensure the count of bookings does not exceed the total number of mechanics
                if (existingBookingsCount >= totalMechanics)
                {
                    return Ok(new { success = false, message = "All available mechanics are booked for this time." });
                }

                var bookingItem = new Booking
                {
                    BikeId = bookingDto.BikeId,
                    BikeChasisNumber = bookingDto.BikeChasisNumber,
                    BikeDescription = bookingDto.BikeDescription,
                    BookingDate = bookingDate,
                    BookingTime = bookingTime,
                    BikeNumber = bookingDto.BikeNumber,
                    BookingAddress = bookingDto.BookingAddress,
                    Total = bookingDto.Total,
                    UserId = bookingDto.UserId,
                    Status = "pending"
                };

                _context.Bookings.Add(bookingItem);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Booking added successfully." });
            }
            catch (FormatException ex)
            {
                _logger.LogError(ex, "Invalid date or time format.");
                return Ok(new { success = false, message = "Invalid date or time format." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding booking");
                return StatusCode(500, new { success = false, message = $"Internal server error: {ex.Message}" });
            }
        }



        [Authorize(Roles = "User")]
        [HttpPut("update")]
        public async Task<IActionResult> UpdateBooking(UpdateBookingDto updateBookingDto)
        {
            try
            {
                // Check if all required fields are provided
                if (string.IsNullOrEmpty(updateBookingDto.BikeChasisNumber) ||
                    string.IsNullOrEmpty(updateBookingDto.BikeDescription) ||
                    string.IsNullOrEmpty(updateBookingDto.BookingDate) ||
                    string.IsNullOrEmpty(updateBookingDto.BookingTime) ||
                    updateBookingDto.Total == null ||
                    string.IsNullOrEmpty(updateBookingDto.BikeNumber) ||
                    string.IsNullOrEmpty(updateBookingDto.BookingAddress) ||
                    updateBookingDto.BookingId == 0)
                {
                    return Ok(new { success = false, message = "Please enter all fields" });
                }

                // Check if UserId exists
                var userExists = await _context.Users.AnyAsync(u => u.Id == updateBookingDto.UserId);
                if (!userExists)
                {
                    return Ok(new { success = false, message = "User not found." });
                }

                var existingBooking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == updateBookingDto.BookingId);
                if (existingBooking == null)
                {
                    return Ok(new { success = false, message = "Booking not found." });
                }

                // Check if the new BikeNumber is already booked by another entry
                var bikeNumberExists = await _context.Bookings
                    .AnyAsync(b => b.BikeNumber == updateBookingDto.BikeNumber &&
                    b.UserId != updateBookingDto.UserId &&
                    b.Id != updateBookingDto.BookingId);
                if (bikeNumberExists)
                {
                    return Ok(new { success = false, message = "Bike number already booked." });
                }

                // Booking date and time validation
                if (!DateOnly.TryParse(updateBookingDto.BookingDate, out DateOnly bookingDate))
                {
                    return Ok(new { success = false, message = "Invalid Booking Date format. Please use yyyy-MM-dd." });
                }

                if (!TimeOnly.TryParse(updateBookingDto.BookingTime, out TimeOnly bookingTime))
                {
                    return Ok(new { success = false, message = "Invalid Booking Time format. Please use HH:mm." });
                }

                var bookingDateTime = new DateTime(bookingDate.Year, bookingDate.Month, bookingDate.Day, bookingTime.Hour, bookingTime.Minute, 0);

                if (bookingDateTime < DateTime.Now)
                {
                    return Ok(new { success = false, message = "Booking time cannot be in the past." });
                }

                var bookingStartTime = bookingDateTime.AddHours(-2);
                var bookingEndTime = bookingDateTime.AddHours(2);

                var bookingStartTimeSpan = bookingStartTime.TimeOfDay;
                var bookingEndTimeSpan = bookingEndTime.TimeOfDay;

                var bookingTimeCheck = _context.Bookings
                    .AsEnumerable()
                    .Where(b => b.BookingDate == bookingDate &&
                                b.BookingTime.HasValue &&
                                b.BookingTime.Value.ToTimeSpan() >= bookingStartTimeSpan &&
                                b.BookingTime.Value.ToTimeSpan() <= bookingEndTimeSpan)
                    .ToList();

                if (bookingTimeCheck.Any())
                {
                    return Ok(new { success = false, message = "Bike already booked for this time." });
                }

                existingBooking.BikeChasisNumber = updateBookingDto.BikeChasisNumber;
                existingBooking.BikeDescription = updateBookingDto.BikeDescription;
                existingBooking.BookingDate = bookingDate;
                existingBooking.BookingTime = bookingTime;
                existingBooking.BikeNumber = updateBookingDto.BikeNumber;
                existingBooking.BookingAddress = updateBookingDto.BookingAddress;
                existingBooking.Total = updateBookingDto.Total;

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Booking updated successfully." });
            }
            catch (FormatException ex)
            {
                _logger.LogError(ex, "Invalid date or time format.");
                return Ok(new { success = false, message = "Invalid date or time format." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating booking.");
                return StatusCode(500, new { success = false, message = $"Internal server error: {ex.Message}" });
            }
        }

        [HttpGet("getall")]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var bookings = await _context.Bookings
                    .Include(b => b.User)
                    .Include(b => b.Bike)  // Include the bike details
                    .Include(b => b.Mechanic)
                    .Select(b => new
                    {
                        b.Id,
                        b.UserId,
                        UserDetails = new
                        {
                            b.User.FullName,
                            b.User.Email,
                            b.User.PhoneNumber
                        },
                        b.BikeId,
                        BikeDetails = new
                        {
                            b.Bike.BikeName,
                            b.Bike.BikeModel,
                            b.Bike.BikePrice,
                            ImageUrl = b.Bike.BikeImage 
                            //ImageUrl = b.Bike.BikeImage != null ? $"{Request.Scheme}://{Request.Host}/BikeProducts/{b.Bike.BikeImage}" : null // Construct image URL
                        },
                        b.MechanicId,
                        MechanicDetails = new
                        {
                            b.Mechanic.Name,
                            b.Mechanic.PhoneNumber
                        },
                        b.BikeChasisNumber,
                        b.BikeDescription,
                        b.BookingDate,
                        b.BookingTime,
                        b.Status,
                        b.Total,
                        b.BikeNumber,
                        b.BookingAddress,
                    })
                    .ToListAsync();

                if (bookings == null || !bookings.Any())
                {
                    return Ok(new { success = false, message = "No bookings found." });
                }

                return Ok(new { success = true, bookings });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving bookings");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        [HttpGet("getall/{userId}")]
        public async Task<IActionResult> GetAllByUserId(int userId)
        {
            try
            {
                var bookings = await _context.Bookings
                    .Where(b => b.UserId == userId)
                    .Include(b => b.User)
                    .Include(b => b.Bike)  // Include the bike details
                    .Include(b => b.Mechanic)
                    .Select(b => new
                    {
                        b.Id,
                        b.UserId,
                        UserDetails = new
                        {
                            b.User.FullName,
                            b.User.Email,
                            b.User.PhoneNumber
                        },
                        b.BikeId,
                        BikeDetails = new
                        {
                            b.Bike.BikeName,
                            b.Bike.BikeModel,
                            b.Bike.BikePrice,
                            ImageUrl = b.Bike.BikeImage
                            //ImageUrl = b.Bike.BikeImage != null ? $"{Request.Scheme}://{Request.Host}/BikeProducts/{b.Bike.BikeImage}" : null // Construct image URL
                        },
                        b.MechanicId,
                        MechanicDetails = new
                        {
                            b.Mechanic.Name,
                            b.Mechanic.PhoneNumber
                        },
                        b.BikeChasisNumber,
                        b.BikeDescription,
                        b.BookingDate,
                        b.BookingTime,
                        b.Status,
                        b.Total,
                        b.BikeNumber,
                        b.BookingAddress,
                    })
                    .ToListAsync();

                if (bookings == null || !bookings.Any())
                {
                    return Ok(new { success = false, message = "No bookings found for the given user." });
                }

                return Ok(new { success = true, bookings });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving bookings for user");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }



        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingBookings()
        {
            try
            {
                var pendingBookings = await _context.Bookings
                    .Include(b => b.User)
                    .Where(b => b.Status.ToLower() == "pending")
                    .Select(b => new
                    {
                        b.Id,
                        b.UserId,
                        UserDetails = new
                        {
                            b.User.FullName,
                            b.User.Email,
                            b.User.PhoneNumber
                        },
                        b.BikeId,
                        b.BikeChasisNumber,
                        b.BikeDescription,
                        b.BookingDate,
                        b.BookingTime,
                        b.Status,
                        b.Total,
                        b.BikeNumber,
                        b.BookingAddress,
                    })
                    .ToListAsync();

                if (!pendingBookings.Any())
                {
                    return Ok(new { success = false, message = "No pending bookings found." });
                }

                return Ok(new { success = true, pendingBookings });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pending bookings");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        [HttpGet("completed")]
        public async Task<IActionResult> GetCompletedBookings()
        {
            try
            {
                var completedBookings = await _context.Bookings
                    .Include(b => b.User)
                    .Where(b => b.Status.ToLower() == "completed")
                    .Select(b => new
                    {
                        b.Id,
                        b.UserId,
                        UserDetails = new
                        {
                            b.User.FullName,
                            b.User.Email,
                            b.User.PhoneNumber
                        },
                        b.BikeId,
                        b.BikeChasisNumber,
                        b.BikeDescription,
                        b.BookingDate,
                        b.BookingTime,
                        b.Status,
                        b.Total,
                        b.BikeNumber,
                        b.BookingAddress,
                    })
                    .ToListAsync();

                if (!completedBookings.Any())
                {
                    return Ok(new { success = false, message = "No completed bookings found." });
                }

                return Ok(new { success = true, completedBookings });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving completed bookings");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }


        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteBookingItem(int id)
        {
            try
            {
                var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == id);
                if (booking == null)
                {
                    return NotFound(new { success = false, message = "Booking not found" });
                }

                var assignedMechanic = await _context.Mechanics.FirstOrDefaultAsync(m => m.IsAssignedTo == id);
                if (assignedMechanic != null)
                {
                    assignedMechanic.IsAssignedTo = null;
                    _context.Mechanics.Update(assignedMechanic);
                }

                _context.Bookings.Remove(booking);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Item deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting booking");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }
        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsersFromBookings()
        {
            try
            {
                var users = await _context.Bookings
                    .Include(b => b.User)
                    .Select(b => new
                    {
                        b.User.Id,
                        b.User.FullName,
                        b.User.Email,
                        b.User.PhoneNumber
                    })
                    .Distinct()
                    .ToListAsync();

                if (!users.Any())
                {
                    return Ok(new { success = false, message = "No users found with bookings." });
                }

                return Ok(new { success = true, users });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users from bookings");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }


        [HttpPost("cancel/{id}")]
        public async Task<IActionResult> CancelBooking(int id)
        {
            try
            {
                var booking = await _context.Bookings.FindAsync(id);

                if (booking == null)
                {
                    return NotFound(new { success = false, message = "Booking not found" });
                }

                booking.Status = "canceled";
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Booking canceled successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error canceling booking");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        [HttpDelete("bookings")]
        public async Task<IActionResult> DeleteAllBookings()
        {
            try
            {
                // Retrieve all bookings
                var bookings = _context.Bookings.ToList();

                if (!bookings.Any())
                {
                    return NotFound(new { message = "No bookings found to delete." });
                }

                // Remove all bookings
                _context.Bookings.RemoveRange(bookings);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "All bookings have been deleted." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal Server Error", error = ex.Message });
            }
        }


    }
}
