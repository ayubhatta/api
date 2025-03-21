﻿using HomeBikeServiceAPI.Data;
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

                // Retrieve all mechanics and filter them in-memory
                var mechanics = _context.Mechanics
                    .AsEnumerable()  // Use AsEnumerable to filter in memory
                    .Where(m => m.IsAssignedTo != null && m.IsAssignedTo.Contains(bookingId))
                    .FirstOrDefault();  // Use FirstOrDefault (synchronous method) instead of FirstOrDefaultAsync

                if (mechanics == null) return; // No mechanic assigned

                // Retrieve the user associated with the booking
                var user = await _context.Users.FindAsync(booking.UserId);
                if (user == null || string.IsNullOrEmpty(user.Email)) return;

                // Construct the email body content
                var emailBody = "<p>Dear User,</p>";
                emailBody += "<p>Your bike servicing has been assigned to a mechanic.</p>";
                emailBody += $"<p><strong>Mechanic Name: {mechanics.Name}</strong></p>";
                emailBody += $"<p><strong>Mechanic Phone: {mechanics.PhoneNumber}</strong></p>";
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

                // If you're querying mechanics here, handle the IsAssignedTo filtering in memory
                // For instance, if you need the mechanic, you could use AsEnumerable to do the filtering in-memory
                var mechanic = _context.Mechanics
                    .AsEnumerable()  // Use AsEnumerable to filter in-memory
                    .FirstOrDefault(m => m.IsAssignedTo.Contains(bookingId));

                if (mechanic == null) return;  // No mechanic assigned

                var emailBody = "<p>Dear User,</p>";
                emailBody += "<p>Your bike servicing is in progress.</p>";
                emailBody += $"<p><strong>Mechanic Name: {mechanic.Name}</strong></p>";
                emailBody += $"<p><strong>Mechanic Phone: {mechanic.PhoneNumber}</strong></p>";
                emailBody += "<p>Regards,<br/>Ride Revive</p>";

                var mailRequest = new MailRequestHelper
                {
                    To = user.Email,
                    Subject = "Bike Servicing In Progress",
                    Body = emailBody
                };

                await _emailService.SendEmailAsync(mailRequest);
                _logger.LogInformation($"In-progress email sent to {user.Email}.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending InProgress email for BookingId {bookingId}: {ex.Message}");
            }
        }


        public async Task CompletedJob(int bookingId, decimal totalAmount)
        {
            try
            {
                var booking = await _context.Bookings
                    .Include(b => b.User)
                    .FirstOrDefaultAsync(b => b.Id == bookingId);
                if (booking == null) return;

                var user = booking.User;
                if (user == null || string.IsNullOrEmpty(user.Email)) return;

                var mechanic = _context.Mechanics
                    .AsEnumerable()
                    .FirstOrDefault(m => m.Id == booking.MechanicId);

                if (mechanic == null) return;

                // Use the totalAmount passed from MarkBookingComplete instead of calling GetTotalAmount again
                var emailBody = $"<p>Dear User,</p>";
                emailBody += "<p>Your bike servicing has been completed.</p>";
                emailBody += $"<p><strong>Total Amount: {totalAmount}</strong></p>";
                emailBody += $"<p><strong>Mechanic Name: {mechanic.Name}</strong></p>";
                emailBody += $"<p><strong>Mechanic Phone: {mechanic.PhoneNumber}</strong></p>";
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
        private async Task<decimal> GetTotalAmount(int bookingId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"http://localhost:5046/api/TotalSum/{bookingId}");

                // Log the raw HTML response content
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning($"Received response for bookingId {bookingId}: {responseContent}");

                // Handle HTML responses gracefully (unexpected content)
                if (response.Content.Headers.ContentType.MediaType.Equals("text/html", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning($"Received unexpected HTML content for UserId {bookingId}: {responseContent}");
                    return 0;
                }

                // If the response is JSON, proceed to deserialize
                var result = JsonSerializer.Deserialize<TotalAmountResponse>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return result?.TotalAmount ?? 0;

            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching total amount for UserId {bookingId}: {ex.Message}");
                return 0;
            }
        }


    }

    public class TotalAmountResponse
    {
        public decimal TotalAmount { get; set; }
    }
}
