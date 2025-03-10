using HomeBikeServiceAPI.Data;
using HomeBikeServiceAPI.Models;
using HomeBikeServiceAPI.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace HomeBikeServiceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FeedbackController : ControllerBase
    {
        private readonly IFeedbackRepo _feedbackRepo;
        private readonly ILogger<FeedbackController> _logger;
        private readonly AppDbContext _context;

        public FeedbackController(IFeedbackRepo feedbackRepo, ILogger<FeedbackController> logger, AppDbContext context)
        {
            _feedbackRepo = feedbackRepo;
            _logger = logger;
            _context = context;
        }

        // Create Feedback
        [HttpPost("add")]
        public async Task<IActionResult> AddFeedback(FeedbackDTO feedbackDto)
        {
            try
            {
                if (feedbackDto == null)
                {
                    return BadRequest(new { success = false, message = "Feedback data is required." });
                }

                // Validate feedback properties
                if (string.IsNullOrEmpty(feedbackDto.Subject) || string.IsNullOrEmpty(feedbackDto.Message))
                {
                    return BadRequest(new { success = false, message = "Feedback subject and message are required." });
                }

                // Check if User exists
                var userExists = await _context.Users.AnyAsync(u => u.Id == feedbackDto.UserId);
                if (!userExists)
                {
                    return BadRequest(new { success = false, message = $"User with id {feedbackDto.UserId} not found." });
                }

                // Map DTO to Feedback entity
                var feedback = new Feedback
                {
                    UserId = feedbackDto.UserId,
                    Subject = feedbackDto.Subject,
                    Message = feedbackDto.Message,
                    Rating = feedbackDto.Rating,
                    CreatedAt = feedbackDto.CreatedAt
                };

                // Add feedback to the database
                await _feedbackRepo.AddFeedbackAsync(feedback);

                // Log success
                _logger.LogInformation("Feedback submitted successfully for User ID: {UserId}", feedbackDto.UserId);

                return Ok(new { success = true, message = "Feedback submitted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding feedback.");
                return StatusCode(500, new { success = false, message = "Internal server error, please try again later." });
            }
        }


        [HttpPut("update/{id}")]
        public async Task<IActionResult> UpdateFeedback(int id, [FromBody] FeedbackDTO feedbackDto)
        {
            try
            {
                if (feedbackDto == null)
                {
                    return BadRequest(new { success = false, message = "Feedback data is required." });
                }

                // Validate feedback properties
                if (string.IsNullOrEmpty(feedbackDto.Subject) || string.IsNullOrEmpty(feedbackDto.Message))
                {
                    return BadRequest(new { success = false, message = "Feedback subject and message are required." });
                }

                var existingFeedback = await _feedbackRepo.GetFeedbackByIdAsync(id);
                if (existingFeedback == null)
                {
                    return NotFound(new { success = false, message = "Feedback not found." });
                }

                // Ensure the user ID matches the existing record (optional security check)
                if (existingFeedback.UserId != feedbackDto.UserId)
                {
                    return BadRequest(new { success = false, message = "User ID mismatch. Cannot update feedback for another user." });
                }

                // Update feedback details
                existingFeedback.Subject = feedbackDto.Subject;
                existingFeedback.Message = feedbackDto.Message;
                existingFeedback.Rating = feedbackDto.Rating;
                existingFeedback.CreatedAt = feedbackDto.CreatedAt; // Update timestamp

                await _feedbackRepo.UpdateFeedbackAsync(existingFeedback);

                // Log success
                _logger.LogInformation("Feedback updated successfully for Feedback ID: {FeedbackId}", id);
                return Ok(new { success = true, message = "Feedback updated successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating feedback.");
                return StatusCode(500, new { success = false, message = "Internal server error, please try again later." });
            }
        }


        // Get All Feedback with User Details
        [HttpGet("all")]
        public async Task<IActionResult> GetAllFeedback()
        {
            try
            {
                var feedbacks = await _context.Feedbacks
                    .Include(f => f.User) // Include user details
                    .ToListAsync();

                if (feedbacks == null || feedbacks.Count == 0)
                {
                    return NotFound(new { success = false, message = "No feedback found." });
                }

                var feedbackResponse = feedbacks.Select(f => new
                {
                    f.Id,
                    f.UserId,
                    User = new
                    {
                        f.User.Id,
                        f.User.FullName,  // Assuming User has a Name property
                        f.User.Email,  // Assuming User has an Email property
                        f.User.PhoneNumber  // Assuming User has a PhoneNumber property
                    },
                    f.Subject,
                    f.Message,
                    f.Rating
                }).ToList();

                return Ok(new { success = true, message = "Feedback retrieved successfully.", data = feedbackResponse });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving feedback.");
                return StatusCode(500, new { success = false, message = "Internal server error, please try again later." });
            }
        }

        // Get Feedback by Id with User Details
        [HttpGet("{id}")]
        public async Task<IActionResult> GetFeedbackById(int id)
        {
            try
            {
                var feedback = await _context.Feedbacks
                    .Include(f => f.User) // Include user details
                    .FirstOrDefaultAsync(f => f.Id == id);

                if (feedback == null)
                {
                    return NotFound(new { success = false, message = "Feedback not found." });
                }

                var feedbackResponse = new
                {
                    feedback.Id,
                    feedback.UserId,
                    User = new
                    {
                        feedback.User.Id,
                        feedback.User.FullName,  // Assuming User has a Name property
                        feedback.User.Email,  // Assuming User has an Email property
                        feedback.User.PhoneNumber  // Assuming User has a PhoneNumber property
                    },
                    feedback.Subject,
                    feedback.Message,
                    feedback.Rating
                    
                };

                return Ok(new { success = true, message = "Feedback retrieved successfully.", data = feedbackResponse });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving feedback.");
                return StatusCode(500, new { success = false, message = "Internal server error, please try again later." });
            }
        }


        // Delete Feedback
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteFeedback(int id)
        {
            try
            {
                var feedback = await _feedbackRepo.GetFeedbackByIdAsync(id);
                if (feedback == null)
                {
                    return NotFound(new { success = false, message = "Feedback not found." });
                }

                await _feedbackRepo.DeleteFeedbackAsync(id);

                // Log success
                _logger.LogInformation("Feedback deleted successfully for Feedback ID: {FeedbackId}", id);
                return Ok(new { success = true, message = "Feedback deleted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting feedback.");
                return StatusCode(500, new { success = false, message = "Internal server error, please try again later." });
            }
        }


        [HttpDelete("delete-all")]
        public async Task<IActionResult> DeleteAll()
        {
            try
            {
                _context.Feedbacks.RemoveRange(_context.Feedbacks);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "All Feedbacks deleted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while deleting all feedbacks.");
                return StatusCode(500, new { success = false, message = "Internal server error.", error = ex.Message });
            }
        }


    }
}
