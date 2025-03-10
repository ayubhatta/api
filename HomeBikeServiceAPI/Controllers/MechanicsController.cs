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
                    fullName = m.Name,
                    isAssignedTo = m.IsAssignedTo,
                    userId = m.UserId,
                    bookingDetails = m.Bookings.Select(b => new  // Iterate over bookings collection
                    {
                        id = b.Id,
                        bookingAddress = b.BookingAddress,
                        bikeChasisNumber = b.BikeChasisNumber,
                        bikeDescription = b.BikeDescription,
                        bookingDate = b.BookingDate?.ToString("yyyy-MM-dd"),
                        bookingTime = b.BookingTime?.ToString(@"hh\:mm\:ss"),
                        status = b.Status,
                        total = b.Total,
                        bikeNumber = b.BikeNumber,
                        userId = b.UserId,
                        userDetails = new
                        {
                            fullName = b.User.FullName,
                            email = b.User.Email,
                            phoneNumber = b.User.PhoneNumber
                        },
                        bikeId = b.BikeId,
                        bikeDetails = b.Bike != null ? new
                        {
                            bikeName = b.Bike.BikeName,
                            bikeModel = b.Bike.BikeModel,
                            bikePrice = b.Bike.BikePrice
                        } : null
                    }).ToList()  // Convert to a list
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
                bookingDetails = mechanic.Bookings.Select(b => new  // Iterate over bookings collection
                {
                    id = b.Id,
                    bookingAddress = b.BookingAddress,
                    bikeChasisNumber = b.BikeChasisNumber,
                    bikeDescription = b.BikeDescription,
                    bookingDate = b.BookingDate?.ToString("yyyy-MM-dd"),
                    bookingTime = b.BookingTime?.ToString(@"hh\:mm\:ss"),
                    status = b.Status,
                    total = b.Total,
                    bikeNumber = b.BikeNumber,
                    userId = b.UserId,
                    userDetails = new
                    {
                        fullName = b.User.FullName,
                        email = b.User.Email,
                        phoneNumber = b.User.PhoneNumber
                    },
                    bikeId = b.BikeId,
                    bikeDetails = b.Bike != null ? new
                    {
                        bikeName = b.Bike.BikeName,
                        bikeModel = b.Bike.BikeModel,
                        bikePrice = b.Bike.BikePrice
                    } : null
                }).ToList()  // Convert to a list of booking details
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
            var existingMechanic = await _context.Mechanics
                .Include(m => m.Bookings) // Include all assigned bookings
                .FirstOrDefaultAsync(m => m.Id == id);

            if (existingMechanic == null)
                return NotFound(new { message = "Mechanic not found." });

            _logger.LogInformation("Mechanic found with ID: {MechanicId}", id);
            _logger.LogInformation("BookingId received: {BookingId}", mechanicDto.IsAssignedTo);

            if (!mechanicDto.IsAssignedTo.HasValue)
                return BadRequest(new { message = "BookingId must be provided." });

            var newBooking = await _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Bike)
                .FirstOrDefaultAsync(b => b.Id == mechanicDto.IsAssignedTo.Value);

            if (newBooking == null)
            {
                _logger.LogWarning("Booking with ID {BookingId} not found", mechanicDto.IsAssignedTo.Value);
                return BadRequest(new { message = "Invalid BookingId provided." });
            }

            if (newBooking.Status != "pending")
            {
                _logger.LogWarning("Booking with ID {BookingId} has status {BookingStatus}. Mechanic can only be assigned to a 'Pending' booking.", mechanicDto.IsAssignedTo.Value, newBooking.Status);
                return BadRequest(new { message = "Booking status is not 'Pending'. Mechanic cannot be assigned." });
            }

            // Check if the booking is already assigned to another mechanic
            if (await _context.Bookings.AnyAsync(b => b.MechanicId != null && b.MechanicId != existingMechanic.Id && b.Id == newBooking.Id))
            {
                _logger.LogWarning("Booking with ID {BookingId} is already assigned to another mechanic.", newBooking.Id);
                return BadRequest(new { message = "The booking is already assigned to another mechanic." });
            }

            // Check for booking time conflict with the mechanic's current assignments
            foreach (var assignedBooking in existingMechanic.Bookings)
            {
                if (assignedBooking.BookingDate == newBooking.BookingDate && assignedBooking.BookingTime.HasValue && newBooking.BookingTime.HasValue)
                {
                    var assignedDateTime = assignedBooking.BookingDate.Value.ToDateTime(assignedBooking.BookingTime.Value);
                    var newBookingDateTime = newBooking.BookingDate.Value.ToDateTime(newBooking.BookingTime.Value);

                    if (Math.Abs((assignedDateTime - newBookingDateTime).TotalHours) < 2)
                    {
                        _logger.LogWarning("Mechanic with ID {MechanicId} is already assigned to a booking within 2 hours.", id);
                        return BadRequest(new { message = "Mechanic cannot be assigned to a booking within 2 hours of an existing booking." });
                    }
                }
            }

            // Assign the new booking to the mechanic
            newBooking.MechanicId = existingMechanic.Id;
            existingMechanic.Bookings.Add(newBooking); // Maintain multiple assignments

            _context.Mechanics.Update(existingMechanic);
            _context.Bookings.Update(newBooking);

            await _context.SaveChangesAsync();

            // Trigger the job (if needed)
            TimeSpan delay = TimeSpan.FromSeconds(1);
            _jobTriggerService.TriggerMechanicAssignedJob(newBooking.Id, delay);

            var response = new
            {
                AssignedMechanicId = existingMechanic.Id,
                AssignedBookings = existingMechanic.Bookings.Select(b => new
                {
                    b.Id,
                    b.BookingAddress,
                    b.BikeChasisNumber,
                    b.BikeDescription,
                    b.BookingDate,
                    b.BookingTime,
                    b.Status,
                    b.Total,
                    b.BikeNumber,
                    UserDetails = new
                    {
                        b.User.FullName,
                        b.User.Email,
                        b.User.PhoneNumber
                    },
                    BikeDetails = new
                    {
                        b.Bike.BikeName,
                        b.Bike.BikeModel,
                        b.Bike.BikePrice
                    }
                }).ToList()
            };

            return Ok(response);
        }






        [HttpGet("assigned")]
        public async Task<IActionResult> GetAssignedMechanics()
        {
            var assignedMechanics = await _context.Mechanics
                .Where(m => m.IsAssignedTo.HasValue)
                .Include(m => m.Bookings) // Ensure the Booking details are loaded
                    .ThenInclude(b => b.User) // Include the User details
                .Include(m => m.Bookings)
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
                BookingDetails = m.Bookings.Select(b => new  // Iterate over bookings collection
                {
                    b.Id,
                    b.BookingAddress,
                    b.BikeChasisNumber,
                    b.BikeDescription,
                    bookingDate = b.BookingDate?.ToString("yyyy-MM-dd"),
                    bookingTime = b.BookingTime?.ToString(@"hh\:mm\:ss"),
                    b.Status,
                    b.Total,
                    b.BikeNumber,
                    UserDetails = new
                    {
                        b.User.FullName,
                        b.User.Email,
                        b.User.PhoneNumber
                    },
                    BikeDetails = new
                    {
                        b.Bike.BikeName,
                        b.Bike.BikeModel,
                        b.Bike.BikePrice
                    }
                }).ToList()  // Convert to a list of booking details
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
                .Include(m => m.Bookings)
                    .ThenInclude(b => b.User) // Include User details for each booking
                .Include(m => m.Bookings)
                    .ThenInclude(b => b.Bike) // Include Bike details for each booking
                .FirstOrDefaultAsync();

            if (mechanic == null)
            {
                return NotFound($"Assigned mechanic with UserId {userId} not found.");
            }

            var response = new
            {
                mechanic.Id,
                mechanic.Name,
                mechanic.PhoneNumber,
                mechanic.IsAssignedTo,
                BookingDetails = mechanic.Bookings != null && mechanic.Bookings.Any() ? mechanic.Bookings.Select(b => new
                {
                    b.Id,
                    b.BookingAddress,
                    b.BikeChasisNumber,
                    b.BikeDescription,
                    bookingDate = b.BookingDate?.ToString("yyyy-MM-dd"),
                    bookingTime = b.BookingTime?.ToString(@"hh\:mm\:ss"),
                    b.Status,
                    b.Total,
                    b.BikeNumber,
                    UserDetails = new
                    {
                        b.User.FullName,
                        b.User.Email,
                        b.User.PhoneNumber
                    },
                    BikeDetails = new
                    {
                        b.Bike.BikeName,
                        b.Bike.BikeModel,
                        b.Bike.BikePrice
                    }
                }).ToList() : null // Handle if no bookings are found
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
