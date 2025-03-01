using System.Net;
using System.Net.Mail;
using HomeBikeServiceAPI.Helpers;
using Microsoft.Extensions.Options;

namespace HomeBikeServiceAPI.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings emailSettings;

        public EmailService(IOptions<EmailSettings> options)
        {
            this.emailSettings = options.Value;
        }

        public async Task SendEmailAsync(MailRequestHelper mailRequest)
        {
            if (!emailSettings.UseSSL)
            {
                throw new InvalidOperationException("Email cannot be sent because SSL is not enabled.");
            }

            var mailMessage = new MailMessage
            {
                From = new MailAddress(emailSettings.Email, emailSettings.DisplayName),
                Subject = mailRequest.Subject,
                Body = mailRequest.Body,
                IsBodyHtml = true
            };

            mailMessage.To.Add(mailRequest.To);

            using var smtpClient = new SmtpClient
            {
                Host = emailSettings.Host,
                Port = emailSettings.Port,
                EnableSsl = emailSettings.UseSSL,
                Credentials = new NetworkCredential(emailSettings.Fullname, emailSettings.Password)
            };

            try
            {
                await smtpClient.SendMailAsync(mailMessage);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to send email: " + ex.Message);
            }
        }
    }
}
