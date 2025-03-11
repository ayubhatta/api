using HomeBikeServiceAPI.Data;
using HomeBikeServiceAPI.DTO;
using HomeBikeServiceAPI.Models;
using HomeBikeServiceAPI.Repositories;
using HomeBikeServiceAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
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
            try
            {
                var mechanics = await _context.Mechanics
                    .Include(m => m.Bookings)
                    .ThenInclude(b => b.User)
                    .Include(m => m.Bookings)
                    .ThenInclude(b => b.Bike)
                    .ToListAsync();

                var response = mechanics.Select(m => new
                {
                    mechanicId = m.Id,
                    fullName = m.Name,
                    phoneNumber = m.PhoneNumber,
                    isAssignedTo = string.IsNullOrEmpty(m.IsAssignedToJson) ? new List<int>() : JsonSerializer.Deserialize<List<int>>(m.IsAssignedToJson),
                    userId = m.UserId,
                    bookingDetails = m.Bookings.Select(b => new
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
                    }).ToList()
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
            var mechanic = await _context.Mechanics
                .Include(m => m.Bookings)
                    .ThenInclude(b => b.User)
                .Include(m => m.Bookings)
                    .ThenInclude(b => b.Bike)
                .FirstOrDefaultAsync(m => m.UserId == userId);

            if (mechanic == null)
            {
                return NotFound(new { success = false, message = "Mechanic not found." });
            }

            // Deserialize assigned bookings
            var assignedBookings = string.IsNullOrEmpty(mechanic.IsAssignedToJson)
                ? new List<int>()
                : JsonSerializer.Deserialize<List<int>>(mechanic.IsAssignedToJson);

            // Fetch carts for users with pending or in-progress bookings (to reduce queries in loop)
            var userIds = mechanic.Bookings
                .Where(b => b.Status == "pending" || b.Status == "In-Progress")
                .Select(b => b.UserId)
                .Distinct()
                .ToList();

            var carts = _context.Carts
                .Where(c => userIds.Contains(c.UserId))
                .Include(c => c.BikeParts)
                .ToList();

            var response = new
            {
                mechanic.Id,
                mechanic.Name,
                mechanic.PhoneNumber,
                IsAssignedTo = assignedBookings,
                BookingDetails = mechanic.Bookings.Select(b => new
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
                        userId = b.UserId,
                        b.User.FullName,
                        b.User.Email,
                        b.User.PhoneNumber,
                        Cart = (b.Status == "pending" || b.Status == "In-Progress")
                            ? carts.Where(c => c.UserId == b.UserId)
                                .Select(c => new
                                {
                                    c.Id,
                                    c.BikePartsId,
                                    c.Quantity,
                                    CartDetails = new
                                    {
                                        c.BikeParts.PartName,
                                        c.BikeParts.Description,
                                        c.BikeParts.Price
                                    }
                                }).ToList()
                            : null
                    },
                    BikeDetails = b.Bike != null ? new
                    {
                        b.Bike.BikeName,
                        b.Bike.BikeModel,
                        b.Bike.BikePrice
                    } : null
                }).ToList()
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
            _logger.LogInformation("BookingIds received: {BookingIds}", string.Join(", ", mechanicDto.IsAssignedTo));

            // Check if IsAssignedTo is null or empty
            if (mechanicDto.IsAssignedTo == null || !mechanicDto.IsAssignedTo.Any())
                return BadRequest(new { message = "At least one BookingId must be provided." });

            // Deserialize the current IsAssignedTo list
            var currentAssignedBookings = existingMechanic.IsAssignedTo ?? new List<int>();

            // Process each BookingId in the IsAssignedTo collection
            foreach (var bookingId in mechanicDto.IsAssignedTo)
            {
                var newBooking = await _context.Bookings
                    .Include(b => b.User)
                    .Include(b => b.Bike)
                    .FirstOrDefaultAsync(b => b.Id == bookingId);

                if (newBooking == null)
                {
                    _logger.LogWarning("Booking with ID {BookingId} not found", bookingId);
                    return BadRequest(new { message = $"Invalid BookingId {bookingId} provided." });
                }

                if (newBooking.Status != "pending")
                {
                    _logger.LogWarning("Booking with ID {BookingId} has status {BookingStatus}. Mechanic can only be assigned to a 'Pending' booking.", bookingId, newBooking.Status);
                    return BadRequest(new { message = $"Booking status for {bookingId} is not 'Pending'. Mechanic cannot be assigned." });
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

                // Add the new bookingId to the current assigned bookings if it's not already in the list
                if (!currentAssignedBookings.Contains(bookingId))
                {
                    currentAssignedBookings.Add(bookingId);
                }
            }

            // Serialize the updated list back into the IsAssignedToJson field
            existingMechanic.IsAssignedTo = currentAssignedBookings;

            _context.Mechanics.Update(existingMechanic);
            await _context.SaveChangesAsync();

            // Trigger the job (if needed)
            TimeSpan delay = TimeSpan.FromSeconds(1);
            _jobTriggerService.TriggerMechanicAssignedJob(mechanicDto.IsAssignedTo.First(), delay);

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
                .Include(m => m.Bookings) // Ensure the Booking details are loaded
                    .ThenInclude(b => b.User) // Include the User details
                .Include(m => m.Bookings)
                    .ThenInclude(b => b.Bike) // Include the Bike details
                .ToListAsync();

            // Now filter mechanics in memory based on IsAssignedTo
            assignedMechanics = assignedMechanics.Where(m => m.IsAssignedTo != null && m.IsAssignedTo.Any()).ToList();


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
            // First, load all mechanics (you may consider limiting the fields to optimize performance)
            var unassignedMechanics = await _context.Mechanics
                .ToListAsync();

            // Now filter mechanics in memory based on IsAssignedTo being null or empty
            unassignedMechanics = unassignedMechanics.Where(m => m.IsAssignedTo == null || !m.IsAssignedTo.Any()).ToList();

            if (!unassignedMechanics.Any())
            {
                return Ok(new { success = true, message = "No unassigned mechanics found.", mechanics = unassignedMechanics });
            }

            return Ok(new { success = true, message = "Unassigned mechanics retrieved successfully.", mechanics = unassignedMechanics });
        }






        [HttpPut("update-status/{userId}")]
        public async Task<IActionResult> UpdateMechanicStatus(int userId, [FromBody] BookingUpdateDto request)
        {
            var mechanic = await _context.Mechanics.FirstOrDefaultAsync(m => m.UserId == userId);
            if (mechanic == null)
                return NotFound(new { message = "Mechanic not found." });

            if (mechanic.IsAssignedTo == null || !mechanic.IsAssignedTo.Contains(request.IsAssignedTo))
                return BadRequest(new { message = "Mechanic is not assigned to this booking." });

            var booking = await _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Bike)
                .FirstOrDefaultAsync(b => b.Id == request.IsAssignedTo);

            if (booking == null)
                return NotFound(new { message = "Associated booking not found." });

            if (booking.Status != "pending")
                return BadRequest(new { message = "Booking status must be 'Pending' to update." });

            booking.Status = "In-Progress";
            _context.Bookings.Update(booking);
            await _context.SaveChangesAsync();

            TimeSpan delay = TimeSpan.FromSeconds(1);
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
                    booking.Status,
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

        public class BookingUpdateDto
        {
            public int IsAssignedTo { get; set; }
        }





        [HttpPut("mark-complete/{userId}")]
        public async Task<IActionResult> MarkBookingComplete(int userId, [FromBody] UpdateBookingStatusDto request)
        {
            // Validate that request is valid and the IsAssignedTo is greater than 0 (since it's a collection, check if it's valid)
            if (request == null || request.IsAssignedTo == null || !request.IsAssignedTo.Any())
                return BadRequest(new { message = "Invalid booking ID." });

            // Fetch the mechanic by userId
            var mechanic = await _context.Mechanics.FirstOrDefaultAsync(m => m.UserId == userId);
            if (mechanic == null)
                return NotFound(new { message = "Mechanic not found." });

            // Check if mechanic is assigned to the booking (IsAssignedTo is a collection)
            if (mechanic.IsAssignedTo == null || !mechanic.IsAssignedTo.Contains(request.IsAssignedTo.First()))
                return BadRequest(new { message = "Mechanic is not assigned to this booking." });

            // Find the booking
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => request.IsAssignedTo.Contains(b.Id));
            if (booking == null)
                return NotFound(new { message = "Associated booking not found." });

            // Check booking status before marking as complete
            if (booking.Status == "pending")
                return BadRequest(new { message = "Booking is still pending. It must be 'In-Progress' before completion." });

            if (booking.Status != "In-Progress")
                return BadRequest(new { message = "Booking status must be 'In-Progress' to mark as 'Complete'." });

            // Mark booking as complete
            booking.Status = "Complete";

            // Calculate total amount for the booking
            var totalAmountResponse = await _totalSumController.GetTotalAmount(booking.UserId);
            if (totalAmountResponse is OkObjectResult okResult)
            {
                var totalAmount = ((dynamic)okResult.Value).TotalAmount;
                booking.Total = totalAmount;
            }
            else
            {
                return StatusCode(500, new { message = "Failed to calculate total amount." });
            }

            // Remove the completed booking Id from the mechanic's IsAssignedTo list (stored as JSON)
            if (mechanic.IsAssignedTo != null)
            {
                // Deserialize the IsAssignedToJson field into a list of integers
                var assignedBookings = mechanic.IsAssignedTo.ToList();

                // Remove the completed booking Id (booking.Id) from the list
                assignedBookings.Remove(booking.Id);

                // Serialize the updated list back into a JSON string
                mechanic.IsAssignedToJson = JsonSerializer.Serialize(assignedBookings);
            }

            // Unassign mechanic if needed
            if (mechanic.IsAssignedTo == null || !mechanic.IsAssignedTo.Any()) // If no bookings left, set to null
            {
                mechanic.IsAssignedToJson = null;
            }

            // Update the booking and mechanic in the database
            _context.Bookings.Update(booking);
            _context.Mechanics.Update(mechanic);
            await _context.SaveChangesAsync();

            // Trigger job for completed booking
            TimeSpan delay = TimeSpan.FromSeconds(1);
            _jobTriggerService.TriggerCompletedJob(booking.Id, delay);

            // Return response
            return Ok(new
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
                    booking.Status,
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
            });
        }





        [HttpGet("assigned/{userId}")]
        public async Task<IActionResult> GetAssignedMechanicById(int userId)
        {
            // First, load all mechanics (you can optimize by limiting the fields you select)
            var mechanic = await _context.Mechanics
                .Include(m => m.Bookings)
                    .ThenInclude(b => b.User) // Include User details for each booking
                .Include(m => m.Bookings)
                    .ThenInclude(b => b.Bike) // Include Bike details for each booking
                .FirstOrDefaultAsync(m => m.UserId == userId);

            // Check if mechanic is found and if they are assigned to any bookings
            if (mechanic == null || mechanic.IsAssignedTo == null || !mechanic.IsAssignedTo.Any())
            {
                return NotFound($"Assigned mechanic with UserId {userId} not found.");
            }

            var response = new
            {
                mechanic.Id,
                mechanic.Name,
                mechanic.PhoneNumber,
                mechanic.IsAssignedTo,
                BookingDetails = mechanic.Bookings.Select(b => new
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
                        userId = b.UserId,
                        b.User.FullName,
                        b.User.Email,
                        b.User.PhoneNumber,
                        Cart =(b.Status == "pending" || b.Status == "In-Progress")?
                            _context.Carts
                            .Where(c => c.UserId == b.UserId)
                            .Include(c => c.BikeParts)
                            .Select(c => new
                            {
                                c.Id,
                                c.BikePartsId,
                                c.Quantity,
                                CartDetails = new
                                {
                                    c.BikeParts.PartName,
                                    c.BikeParts.Description,
                                    c.BikeParts.Price
                                }
                            }).ToList()
                            : null
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



        [HttpGet("unassigned/{userId}")]
        public async Task<IActionResult> GetUnassignedMechanicById(int userId)
        {
            // First, load all mechanics
            var mechanic = await _context.Mechanics
                .FirstOrDefaultAsync(m => m.UserId == userId);

            // Check if mechanic is found and if they are unassigned
            if (mechanic == null || mechanic.IsAssignedTo != null && mechanic.IsAssignedTo.Any())
            {
                return NotFound($"Unassigned mechanic with UserId {userId} not found.");
            }

            return Ok(mechanic);
        }




        [HttpDelete("{Id}")]
        public async Task<IActionResult> DeleteMechanic(int Id)
        {
            var isDeleted = await _mechanicRepository.DeleteMechanicAsync(Id);
            if (!isDeleted)
                return NotFound(new { message = "Mechanic not found." });
            return NoContent(); // Respond with no content after deletion
        }



        [HttpDelete("delete-all")]
        public async Task<IActionResult> DeleteAllUsers()
        {
            try
            {
                // Step 1: Delete all related records in the Bookings table
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM Bookings");

                // Step 2: Delete all related Mechanics records
                //await _context.Database.ExecuteSqlRawAsync("DELETE FROM Mechanics");

                // Step 3: Optionally, delete all Users (if needed)
                // await _context.Database.ExecuteSqlRawAsync("DELETE FROM Users");

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "All users and related data have been deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred while deleting users.", error = ex.Message });
            }
        }


    }

}
