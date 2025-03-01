using Hangfire;
using HomeBikeServiceAPI.Data;
using HomeBikeServiceAPI.DTO;
using HomeBikeServiceAPI.Helpers;
using HomeBikeServiceAPI.Models;
using HomeBikeServiceAPI.Repositories;
using HomeBikeServiceAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

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
            var mechanics = await _mechanicRepository.GetAllMechanicsAsync();
            return Ok(mechanics);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetMechanicById(int id)
        {
            var mechanic = await _mechanicRepository.GetMechanicByIdAsync(id);
            if (mechanic == null) return NotFound(new { message = "Mechanic not found." });

            return Ok(mechanic);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMechanicAsync(int id, MechanicUpdateDto mechanicDto)
        {
            // Step 1: Retrieve the mechanic from the database
            var existingMechanic = await _context.Mechanics.FindAsync(id);
            if (existingMechanic == null)
                return NotFound(new { message = "Mechanic not found." });

            // Log the received mechanic ID and BookingId
            _logger.LogInformation("Mechanic found with ID: {MechanicId}", id);
            _logger.LogInformation("BookingId received: {BookingId}", mechanicDto.IsAssignedTo);

            // Step 2: Check if the mechanic is already assigned to a booking
            if (existingMechanic.IsAssignedTo.HasValue)
            {
                _logger.LogWarning("Mechanic with ID {MechanicId} is already assigned to booking ID {BookingId}", id, existingMechanic.IsAssignedTo.Value);
                return BadRequest(new { message = "Mechanic is already assigned to a booking." });
            }

            // Declare the booking variable (without initializing it to null)
            Booking booking = null;

            // Step 3: If IsAssignedTo (BookingId) is provided, check if it's valid
            if (mechanicDto.IsAssignedTo.HasValue)
            {
                // Log the attempt to fetch the booking
                _logger.LogInformation("Attempting to fetch booking with ID: {BookingId}", mechanicDto.IsAssignedTo.Value);

                // Fetch the booking using the provided BookingId
                booking = await _context.Bookings
                    .Include(b => b.User)
                    .Include(b => b.Bike)
                    .FirstOrDefaultAsync(b => b.Id == mechanicDto.IsAssignedTo.Value);

                // Display the fetched booking details in the logs
                if (booking == null)
                {
                    _logger.LogWarning("Booking with ID {BookingId} not found", mechanicDto.IsAssignedTo.Value);
                    return BadRequest(new { message = "Invalid BookingId provided." });
                }

                // Step 4: Check if the booking status is "Pending"
                if (booking.Status != "pending")
                {
                    _logger.LogWarning("Booking with ID {BookingId} has status {BookingStatus}. Mechanic can only be assigned to a 'Pending' booking.", mechanicDto.IsAssignedTo.Value, booking.Status);
                    return BadRequest(new { message = "Booking status is not 'Pending'. Mechanic cannot be assigned." });
                }

                // Step 5: Update the mechanic with the valid BookingId
                existingMechanic.IsAssignedTo = mechanicDto.IsAssignedTo.Value;
            }

            // Step 6: Save changes to the database
            _context.Mechanics.Update(existingMechanic);
            await _context.SaveChangesAsync();

            // Step 7: Prepare the response with mechanic and booking details
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



        [HttpPut("update-status/{id}")]
        public async Task<IActionResult> UpdateMechanicStatus(int id)
        {
            // Step 1: Retrieve the mechanic from the database
            var mechanic = await _context.Mechanics
                .FirstOrDefaultAsync(m => m.Id == id);

            if (mechanic == null)
                return NotFound(new { message = "Mechanic not found." });

            // Step 2: Check if the mechanic is assigned to any booking
            if (!mechanic.IsAssignedTo.HasValue)
                return BadRequest(new { message = "Mechanic is not assigned to any booking." });

            // Step 3: Retrieve the associated booking with User and Bike details
            var booking = await _context.Bookings
                .Include(b => b.User)  // Ensure User details are included
                .Include(b => b.Bike)  // Ensure Bike details are included
                .FirstOrDefaultAsync(b => b.Id == mechanic.IsAssignedTo.Value);

            if (booking == null)
                return NotFound(new { message = "Associated booking not found." });

            // Step 4: Check if the booking status is "Pending"
            if (booking.Status != "pending")
                return BadRequest(new { message = "Booking status must be 'Pending' to update." });

            // Step 5: Update the booking status to "In-Progress"
            booking.Status = "In-Progress";
            _context.Bookings.Update(booking);
            await _context.SaveChangesAsync();

            // Step 6: Define the delay
            TimeSpan delay = TimeSpan.FromSeconds(1); // You can change the delay time as needed

            // Step 7: Trigger the background job with the delay
            _jobTriggerService.TriggerInProgressJob(booking.Id, delay);

            // Step 8: Prepare the response with updated booking details
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


        [HttpPut("mark-complete/{id}")]
        public async Task<IActionResult> MarkBookingComplete(int id)
        {
            // Step 1: Retrieve the mechanic from the database
            var mechanic = await _context.Mechanics
                .FirstOrDefaultAsync(m => m.Id == id);

            if (mechanic == null)
                return NotFound(new { message = "Mechanic not found." });

            // Step 2: Check if the mechanic is assigned to any booking
            if (!mechanic.IsAssignedTo.HasValue)
                return BadRequest(new { message = "Mechanic is not assigned to any booking." });

            // Step 3: Retrieve the associated booking
            var booking = await _context.Bookings.FindAsync(mechanic.IsAssignedTo.Value);
            if (booking == null)
                return NotFound(new { message = "Associated booking not found." });

            // Step 4: Check if the booking status is "Pending" or "In-Progress"
            if (booking.Status == "pending")
                return BadRequest(new { message = "Booking is still pending. It must be 'In-Progress' before completion." });

            if (booking.Status != "In-Progress")
                return BadRequest(new { message = "Booking status must be 'In-Progress' to mark as 'Complete'." });

            // Step 5: Update the booking status to "Complete"
            booking.Status = "Complete";

            // Step 6: Call the TotalSumController to get the total amount and update the booking's Total field
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

            // Step 7: Remove assignment from mechanic (set IsAssignedTo to null)
            mechanic.IsAssignedTo = null;

            // Save changes to database
            _context.Bookings.Update(booking);
            _context.Mechanics.Update(mechanic);
            await _context.SaveChangesAsync();

            // Step 8: Define the delay
            TimeSpan delay = TimeSpan.FromSeconds(1); // Adjust the delay time as needed

            // Step 9: Trigger the background job with the delay
            _jobTriggerService.TriggerCompletedJob(booking.Id, delay);

            // Step 10: Prepare the response with updated booking details
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





        // GET: api/mechanics/assigned/{id}
        [HttpGet("assigned/{id}")]
        public async Task<IActionResult> GetAssignedMechanicById(int id)
        {
            var mechanic = await _context.Mechanics
                .FirstOrDefaultAsync(m => m.Id == id && m.IsAssignedTo.HasValue);

            if (mechanic == null)
            {
                return NotFound($"Assigned mechanic with ID {id} not found.");
            }

            return Ok(mechanic);
        }

        // GET: api/mechanics/unassigned/{id}
        [HttpGet("unassigned/{id}")]
        public async Task<IActionResult> GetUnassignedMechanicById(int id)
        {
            var mechanic = await _context.Mechanics
                .FirstOrDefaultAsync(m => m.Id == id && (!m.IsAssignedTo.HasValue || m.IsAssignedTo.Value == 0));

            if (mechanic == null)
            {
                return NotFound($"Unassigned mechanic with ID {id} not found.");
            }

            return Ok(mechanic);
        }





        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")] // Only admins can delete mechanics
        public async Task<IActionResult> DeleteMechanic(int id)
        {
            var isDeleted = await _mechanicRepository.DeleteMechanicAsync(id);
            if (!isDeleted)
                return NotFound(new { message = "Mechanic not found." });

            return NoContent();
        }
    }
}
