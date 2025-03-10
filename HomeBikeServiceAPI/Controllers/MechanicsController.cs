using HomeBikeServiceAPI.Data;
using HomeBikeServiceAPI.DTO;
using HomeBikeServiceAPI.Models;
using HomeBikeServiceAPI.Repositories;
using HomeBikeServiceAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;


namespace HomeBikeServiceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MechanicsController : ControllerBase
    {
        private readonly IMechanicRepository _mechanicRepository;
        private readonly IBookingRepo _bookingRepository;
        private readonly TotalSumController _totalSumController;
        private readonly IEmailService _emailService;
        private readonly JobTriggerService _jobTriggerService;
        private readonly AppDbContext _context;
        private readonly ILogger<MechanicsController> _logger;

        public MechanicsController(IMechanicRepository mechanicRepository, IBookingRepo bookingRepository, IEmailService emailService, 
                                       JobTriggerService jobTriggerService, AppDbContext context, ILogger<MechanicsController> logger, 
                                       TotalSumController totalSumController)
        {
            _mechanicRepository = mechanicRepository;
            _bookingRepository = bookingRepository;
            _emailService = emailService;
            _jobTriggerService = jobTriggerService;
            _context = context;
            _logger = logger;
            _totalSumController = totalSumController;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllMechanics()
        {
            try
            {
                var mechanics = await _mechanicRepository.GetAllMechanicsAsync();

                var response = mechanics.Select(m => new
                {
                    mechanicId = m.Id,
                    fullName = m.Name,  // Assuming 'FullName' property exists in the 'Mechanic' model
                    isAssignedTo = m.IsAssignedTo,
                    bookingDetails = new
                    {
                        id = m.Booking.Id,
                        bookingAddress = m.Booking.BookingAddress,
                        bikeChasisNumber = m.Booking.BikeChasisNumber,
                        bikeDescription = m.Booking.BikeDescription,
                        bookingDate = m.Booking.BookingDate?.ToString("yyyy-MM-dd"),
                        bookingTime = m.Booking.BookingTime?.ToString(@"hh\:mm\:ss"),
                        status = m.Booking.Status,
                        total = m.Booking.Total,
                        bikeNumber = m.Booking.BikeNumber,
                        userId = m.Booking.UserId,
                        userDetails = new
                        {
                            fullName = m.Booking.User.FullName,
                            email = m.Booking.User.Email,
                            phoneNumber = m.Booking.User.PhoneNumber
                        },
                        bikeId = m.Booking.BikeId,
                        bikeDetails = new
                        {
                            bikeName = m.Booking.Bike.BikeName,
                            bikeModel = m.Booking.Bike.BikeModel,
                            bikePrice = m.Booking.Bike.BikePrice
                        }
                    },
                    userId = m.UserId
                });

                return Ok(new
                {
                    success = true,
                    message = "Mechanics retrieved successfully.",
                    data = response
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = $"An error occurred while fetching mechanics: {ex.Message}",
                    data = (object)null
                });
            }
        }




        [HttpGet("{userId}")]
        public async Task<IActionResult> GetMechanicById(int userId)
        {
            var mechanic = await _mechanicRepository.GetMechanicByIdAsync(userId);
            if (mechanic == null) return NotFound(new { message = "Mechanic not found." });

            var response = new
            {
                mechanicId = mechanic.Id,
                fullName = mechanic.Name,  // Assuming 'FullName' property exists in the 'Mechanic' model
                isAssignedTo = mechanic.IsAssignedTo,
                bookingDetails = new
                {
                    id = mechanic.Booking.Id,
                    bookingAddress = mechanic.Booking.BookingAddress,
                    bikeChasisNumber = mechanic.Booking.BikeChasisNumber,
                    bikeDescription = mechanic.Booking.BikeDescription,
                    bookingDate = mechanic.Booking.BookingDate?.ToString("yyyy-MM-dd"),
                    bookingTime = mechanic.Booking.BookingTime?.ToString(@"hh\:mm\:ss"),
                    status = mechanic.Booking.Status,
                    total = mechanic.Booking.Total,
                    bikeNumber = mechanic.Booking.BikeNumber,
                    userId = mechanic.Booking.UserId,
                    userDetails = new
                    {
                        fullName = mechanic.Booking.User.FullName,
                        email = mechanic.Booking.User.Email,
                        phoneNumber = mechanic.Booking.User.PhoneNumber
                    },
                    bikeId = mechanic.Booking.BikeId,
                    bikeDetails = new
                    {
                        bikeName = mechanic.Booking.Bike.BikeName,
                        bikeModel = mechanic.Booking.Bike.BikeModel,
                        bikePrice = mechanic.Booking.Bike.BikePrice
                    }
                }
            };

            return Ok(new
            {
                success = true,
                message = "Mechanic details retrieved successfully.",
                data = response
            });
        }




        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMechanicAsync(int id, MechanicUpdateDto mechanicDto)
        {
            var existingMechanic = await _context.Mechanics.FindAsync(id);
            if (existingMechanic == null)
                return NotFound(new { message = "Mechanic not found." });

            _logger.LogInformation("Mechanic found with ID: {MechanicId}", id);
            _logger.LogInformation("BookingId received: {BookingId}", mechanicDto.IsAssignedTo);

            // Check if the booking is already assigned to another mechanic
            if (mechanicDto.IsAssignedTo.HasValue)
            {
                var bookingAssignedToAnotherMechanic = await _context.Mechanics
                    .AnyAsync(m => m.IsAssignedTo == mechanicDto.IsAssignedTo.Value && m.Id != existingMechanic.Id);

                if (bookingAssignedToAnotherMechanic)
                {
                    _logger.LogWarning("Booking with ID {BookingId} is already assigned to another mechanic.", mechanicDto.IsAssignedTo.Value);
                    return BadRequest(new { message = "The booking is already assigned to another mechanic." });
                }
            }

            if (existingMechanic.IsAssignedTo.HasValue)
            {
                _logger.LogWarning("Mechanic with ID {MechanicId} is already assigned to booking ID {BookingId}", id, existingMechanic.IsAssignedTo.Value);
                return BadRequest(new { message = "Mechanic is already assigned to a booking." });
            }

            Booking booking = null;
            if (mechanicDto.IsAssignedTo.HasValue)
            {
                _logger.LogInformation("Attempting to fetch booking with ID: {BookingId}", mechanicDto.IsAssignedTo.Value);
                booking = await _context.Bookings
                    .Include(b => b.User)
                    .Include(b => b.Bike)
                    .FirstOrDefaultAsync(b => b.Id == mechanicDto.IsAssignedTo.Value);
                if (booking == null)
                {
                    _logger.LogWarning("Booking with ID {BookingId} not found", mechanicDto.IsAssignedTo.Value);
                    return BadRequest(new { message = "Invalid BookingId provided." });
                }
                if (booking.Status != "pending")
                {
                    _logger.LogWarning("Booking with ID {BookingId} has status {BookingStatus}. Mechanic can only be assigned to a 'Pending' booking.", mechanicDto.IsAssignedTo.Value, booking.Status);
                    return BadRequest(new { message = "Booking status is not 'Pending'. Mechanic cannot be assigned." });
                }

                // Assign the booking to the mechanic
                existingMechanic.IsAssignedTo = mechanicDto.IsAssignedTo.Value;

                // Update the MechanicId in the Booking table
                booking.MechanicId = existingMechanic.Id; // Set MechanicId to the mechanic's Id
            }

            _context.Mechanics.Update(existingMechanic);
            _context.Bookings.Update(booking); // Ensure the booking is updated

            await _context.SaveChangesAsync();

            TimeSpan delay = TimeSpan.FromSeconds(1); // You can change the delay time as needed
            _jobTriggerService.TriggerMechanicAssignedJob(booking.Id, delay);

            var response = new
            {
                existingMechanic.IsAssignedTo,
                BookingDetails = booking != null ? new
                {
                    booking.Id,
                    booking.BookingAddress,
                    booking.BikeChasisNumber,
                    booking.BikeDescription,
                    booking.BookingDate,
                    booking.BookingTime,
                    booking.Status,
                    booking.Total,
                    booking.BikeNumber,
                    UserDetails = new
                    {
                        booking.User.FullName,
                        booking.User.Email,
                        booking.User.PhoneNumber
                    },
                    BikeDetails = new
                    {
                        booking.Bike.BikeName,
                        booking.Bike.BikeModel,
                        booking.Bike.BikePrice
                    }
                } : null
            };

            return Ok(response);
        }

        [HttpGet("assigned")]
        public async Task<IActionResult> GetAssignedMechanics()
        {
            var assignedMechanics = await _context.Mechanics
                .Where(m => m.IsAssignedTo.HasValue)
                .Include(m => m.Booking) // Ensure the Booking details are loaded
                    .ThenInclude(b => b.User) // Include the User details
                .Include(m => m.Booking)
                    .ThenInclude(b => b.Bike) // Include the Bike details
                .ToListAsync();

            if (!assignedMechanics.Any())
            {
                return Ok(new { success = true, message = "No assigned mechanics found.", mechanics = assignedMechanics });
            }
            var response = assignedMechanics.Select(m => new
            {
                m.Id,
                m.Name,
                m.PhoneNumber,
                m.IsAssignedTo,
                BookingDetails = m.Booking != null ? new
                {
                    m.Booking.Id,
                    m.Booking.BookingAddress,
                    m.Booking.BikeChasisNumber,
                    m.Booking.BikeDescription,
                    m.Booking.BookingDate,
                    m.Booking.BookingTime,
                    m.Booking.Status,
                    m.Booking.Total,
                    m.Booking.BikeNumber,
                    UserDetails = new
                    {
                        m.Booking.User.FullName,
                        m.Booking.User.Email,
                        m.Booking.User.PhoneNumber
                    },
                    BikeDetails = new
                    {
                        m.Booking.Bike.BikeName,
                        m.Booking.Bike.BikeModel,
                        m.Booking.Bike.BikePrice
                    }
                } : null
            }).ToList();

            return Ok(new { success = true, message = "Assigned mechanics retrieved successfully.", mechanics = response });
        }


        [HttpGet("unassigned")]
        public async Task<IActionResult> GetUnassignedMechanics()
        {
            var unassignedMechanics = await _context.Mechanics
                .Where(m => !m.IsAssignedTo.HasValue)
                .ToListAsync();

            if (!unassignedMechanics.Any())
            {
                return Ok(new { success = true, message = "No unassigned mechanics found.", mechanics = unassignedMechanics });
            }
            return Ok(new { success = true, message = "Unassigned mechanics retrieved successfully.", mechanics = unassignedMechanics });
        }


        [HttpPut("update-status/{userId}")]
        public async Task<IActionResult> UpdateMechanicStatus(int userId)
        {
            var mechanic = await _context.Mechanics
                .FirstOrDefaultAsync(m => m.UserId == userId);
            if (mechanic == null)
                return NotFound(new { message = "Mechanic not found." });
            if (!mechanic.IsAssignedTo.HasValue)
                return BadRequest(new { message = "Mechanic is not assigned to any booking." });
            var booking = await _context.Bookings
                .Include(b => b.User)  // Ensure User details are included
                .Include(b => b.Bike)  // Ensure Bike details are included
                .FirstOrDefaultAsync(b => b.Id == mechanic.IsAssignedTo.Value);

            if (booking == null)
                return NotFound(new { message = "Associated booking not found." });
            if (booking.Status != "pending")
                return BadRequest(new { message = "Booking status must be 'Pending' to update." });
            booking.Status = "In-Progress";
            _context.Bookings.Update(booking);
            await _context.SaveChangesAsync();
            TimeSpan delay = TimeSpan.FromSeconds(1); // You can change the delay time as needed
            _jobTriggerService.TriggerInProgressJob(booking.Id, delay);
            var response = new
            {
                message = "Booking status updated to 'In-Progress'.",
                mechanicId = mechanic.Id,
                bookingDetails = new
                {
                    booking.Id,
                    booking.BookingAddress,
                    booking.BikeChasisNumber,
                    booking.BikeDescription,
                    booking.BookingDate,
                    booking.BookingTime,
                    booking.Status, // Now updated to "In-Progress"
                    booking.Total,
                    booking.BikeNumber,
                    UserDetails = booking.User != null ? new
                    {
                        booking.User.FullName,
                        booking.User.Email,
                        booking.User.PhoneNumber
                    } : null,

                    BikeDetails = booking.Bike != null ? new
                    {
                        booking.Bike.BikeName,
                        booking.Bike.BikeModel,
                        booking.Bike.BikePrice
                    } : null
                }
            };
            return Ok(response);
        }

        [HttpPut("mark-complete/{userId}")]
        public async Task<IActionResult> MarkBookingComplete(int userId)
        {
            var mechanic = await _context.Mechanics
                .FirstOrDefaultAsync(m => m.UserId == userId);
            if (mechanic == null)
                return NotFound(new { message = "Mechanic not found." });
            if (!mechanic.IsAssignedTo.HasValue)
                return BadRequest(new { message = "Mechanic is not assigned to any booking." });
            var booking = await _context.Bookings.FindAsync(mechanic.IsAssignedTo.Value);
            if (booking == null)
                return NotFound(new { message = "Associated booking not found." });
            if (booking.Status == "pending")
                return BadRequest(new { message = "Booking is still pending. It must be 'In-Progress' before completion." });
            if (booking.Status != "In-Progress")
                return BadRequest(new { message = "Booking status must be 'In-Progress' to mark as 'Complete'." });
            booking.Status = "Complete";
            var totalAmountResponse = await _totalSumController.GetTotalAmount(booking.UserId);
            if (totalAmountResponse is OkObjectResult okResult)
            {
                var totalAmount = ((dynamic)okResult.Value).TotalAmount;
                booking.Total = totalAmount;  // Update the booking's total field
            }
            else
            {
                return StatusCode(500, new { message = "Failed to calculate total amount." });
            }
            mechanic.IsAssignedTo = null;
            _context.Bookings.Update(booking);
            _context.Mechanics.Update(mechanic);
            await _context.SaveChangesAsync();
            TimeSpan delay = TimeSpan.FromSeconds(1); // Adjust the delay time as needed
            _jobTriggerService.TriggerCompletedJob(booking.Id, delay);
            var response = new
            {
                message = "Booking status updated to 'Complete' and mechanic unassigned.",
                mechanicId = mechanic.Id,
                bookingDetails = new
                {
                    booking.Id,
                    booking.BookingAddress,
                    booking.BikeChasisNumber,
                    booking.BikeDescription,
                    booking.BookingDate,
                    booking.BookingTime,
                    booking.Status, // Now updated to "Complete"
                    booking.Total,
                    booking.BikeNumber,
                    UserDetails = booking.User != null ? new
                    {
                        booking.User.FullName,
                        booking.User.Email,
                        booking.User.PhoneNumber
                    } : null,
                    BikeDetails = booking.Bike != null ? new
                    {
                        booking.Bike.BikeName,
                        booking.Bike.BikeModel,
                        booking.Bike.BikePrice
                    } : null
                }
            };

            return Ok(response);
        }

        // GET: api/mechanics/assigned/{userId}
        [HttpGet("assigned/{userId}")]
        public async Task<IActionResult> GetAssignedMechanicById(int userId)
        {
            var mechanic = await _context.Mechanics
                .Where(m => m.UserId == userId && m.IsAssignedTo.HasValue)
                .Include(m => m.Booking)
                    .ThenInclude(b => b.User)
                .Include(m => m.Booking)
                    .ThenInclude(b => b.Bike)
                .FirstOrDefaultAsync();

            if (mechanic == null)
            {
                return NotFound($"Assigned mechanic with ID {userId} not found.");
            }

            var response = new
            {
                mechanic.Id,
                mechanic.Name,
                mechanic.PhoneNumber,
                mechanic.IsAssignedTo,
                BookingDetails = mechanic.Booking != null ? new
                {
                    mechanic.Booking.Id,
                    mechanic.Booking.BookingAddress,
                    mechanic.Booking.BikeChasisNumber,
                    mechanic.Booking.BikeDescription,
                    mechanic.Booking.BookingDate,
                    mechanic.Booking.BookingTime,
                    mechanic.Booking.Status,
                    mechanic.Booking.Total,
                    mechanic.Booking.BikeNumber,
                    UserDetails = new
                    {
                        mechanic.Booking.User.FullName,
                        mechanic.Booking.User.Email,
                        mechanic.Booking.User.PhoneNumber
                    },
                    BikeDetails = new
                    {
                        mechanic.Booking.Bike.BikeName,
                        mechanic.Booking.Bike.BikeModel,
                        mechanic.Booking.Bike.BikePrice
                    }
                } : null
            };

            return Ok(response);
        }


        // GET: api/mechanics/unassigned/{id}
        [HttpGet("unassigned/{userId}")]
        public async Task<IActionResult> GetUnassignedMechanicById(int userId)
        {
            var mechanic = await _context.Mechanics
                .FirstOrDefaultAsync(m => m.UserId == userId && (!m.IsAssignedTo.HasValue || m.IsAssignedTo.Value == 0));

            if (mechanic == null)
            {
                return NotFound($"Unassigned mechanic with ID {userId} not found.");
            }
            return Ok(mechanic);
        }



        [HttpDelete("{userId}")]
        [Authorize(Roles = "Admin")] // Only admins can delete mechanics
        public async Task<IActionResult> DeleteMechanic(int userId)
        {
            var isDeleted = await _mechanicRepository.DeleteMechanicAsync(userId);
            if (!isDeleted)
                return NotFound(new { message = "Mechanic not found." });
            return NoContent();
        }


        [HttpDelete("delete-all")]
        public async Task<IActionResult> DeleteAllUsers()
        {
            try
            {
                // Step 1: Delete all related records in the Bookings table
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM Bookings");

                // Step 2: Delete all Users
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM Users");

                return Ok(new { success = true, message = "All users have been deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred while deleting users.", error = ex.Message });
            }
        }

    }

}
