using HomeBikeServiceAPI.Helpers;

namespace HomeBikeServiceAPI.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(MailRequestHelper mailrequest);

    }
}
