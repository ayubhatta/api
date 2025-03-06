using HomeBikeServiceAPI.Data;
using HomeBikeServiceAPI.Helpers;
using HomeBikeServiceAPI.Models;
using HomeBikeServiceAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http.Json;


namespace HomeBikeServiceAPI.BackgroundServices
{
    public class JobService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<JobService> _logger;
        private readonly IEmailService _emailService;
        private readonly HttpClient _httpClient;

        public JobService(AppDbContext appDbContext, ILogger<JobService> logger, IEmailService emailService, HttpClient httpClient)
        {
            _context = appDbContext;
            _logger = logger;
            _emailService = emailService;
            _httpClient = httpClient;
        }



        // Job for Mechanic Assigned status
        public async Task MechanicAssignedJob(int bookingId)
        {
            try
            {
                // Retrieve the booking details
                var booking = await _context.Bookings.FindAsync(bookingId);
                if (booking == null) return;

                // Retrieve the mechanic assigned to the booking
                var mechanic = await _context.Mechanics.FirstOrDefaultAsync(m => m.IsAssignedTo == bookingId);
                if (mechanic == null) return; // No mechanic assigned

                // Retrieve the user associated with the booking
                var user = await _context.Users.FindAsync(booking.UserId);
                if (user == null || string.IsNullOrEmpty(user.Email)) return;

                // Construct the email body content
                var emailBody = "<p>Dear User,</p>";
                emailBody += "<p>Your bike servicing has been assigned to a mechanic.</p>";
                emailBody += $"<p><strong>Mechanic Name: {mechanic.Name}</strong></p>";
                emailBody += $"<p><strong>Mechanic Phone: {mechanic.PhoneNumber}</strong></p>";
                emailBody += "<p>We will notify you once the service begins and is completed.</p>";
                emailBody += "<p>Regards,<br/>Ride Revive</p>";

                // Prepare the mail request
                var mailRequest = new MailRequestHelper
                {
                    To = user.Email,
                    Subject = "Bike Servicing Assigned to Mechanic",
                    Body = emailBody
                };

                // Send the email
                await _emailService.SendEmailAsync(mailRequest);
                _logger.LogInformation($"Mechanic assignment email sent to {user.Email}.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending Mechanic Assigned email for BookingId {bookingId}: {ex.Message}");
            }
        }




        // Job for In Progress status
        public async Task InProgressJob(int bookingId)
        {
            try
            {
                var booking = await _context.Bookings.FindAsync(bookingId);
                if (booking == null) return;

                var user = await _context.Users.FindAsync(booking.UserId);
                if (user == null || string.IsNullOrEmpty(user.Email)) return;

                var emailBody = "<p>Dear User,</p>";
                emailBody += "<p>Your bike servicing is in progress.</p>";
                emailBody += "<p>Regards,<br/>Ride Revive</p>";

                

                var mailRequest = new MailRequestHelper
                {
                    To = user.Email,
                    Subject = "Bike Servicing In Progress",
                    Body = emailBody
                };

                await _emailService.SendEmailAsync(mailRequest);
                _logger.LogInformation($"In-progress email sent to {user.Email}.");

                Console.WriteLine($"Sending email to user {booking.UserId} that the service is in progress.");
                // Call your email service here


            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending InProgress email for BookingId {bookingId}: {ex.Message}");
            }


        }

        // Job for Completed status
        public async Task CompletedJob(int bookingId)
        {
            try
            {
                var booking = await _context.Bookings.FindAsync(bookingId);
                if (booking == null) return;

                var user = await _context.Users.FindAsync(booking.UserId);
                if (user == null || string.IsNullOrEmpty(user.Email)) return;

                // Fetch the total amount from the TotalSum API
                decimal totalAmount = await GetTotalAmount(user.Id);

                var emailBody = "<p>Dear User,</p>";
                emailBody += "<p>Your bike servicing has been completed.</p>";
                emailBody += $"<p><strong>Total Amount: {totalAmount}</strong></p>";
                emailBody += "<p>Regards,<br/>Ride Revive</p>";

                var mailRequest = new MailRequestHelper
                {
                    To = user.Email,
                    Subject = "Bike Servicing Completed",
                    Body = emailBody
                };

                await _emailService.SendEmailAsync(mailRequest);
                _logger.LogInformation($"Completed email sent to {user.Email}.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending Completed email for BookingId {bookingId}: {ex.Message}");
            }
        }

        // Helper method to get total amount from API
        private async Task<decimal> GetTotalAmount(int userId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"https://api-rj9q.onrender.com/api/TotalSum/{userId}");

                // Log the raw HTML response content
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning($"Received response for UserId {userId}: {responseContent}");

                // Handle HTML responses gracefully (unexpected content)
                if (response.Content.Headers.ContentType.MediaType.Equals("text/html", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning($"Received unexpected HTML content for UserId {userId}: {responseContent}");
                    return 0;
                }

                // If the response is JSON, proceed to deserialize
                var result = JsonSerializer.Deserialize<TotalAmountResponse>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return result?.TotalAmount ?? 0;

            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching total amount for UserId {userId}: {ex.Message}");
                return 0;
            }
        }


    }

    public class TotalAmountResponse
    {
        public decimal TotalAmount { get; set; }
    }
}
