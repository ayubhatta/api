using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace HomeBikeServiceAPI.Helpers
{
    public class EmailSettings
    {
        public string Fullname { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string Host { get; set; }
        public string DisplayName { get; set; }
        public bool UseSSL { get; set; }
        public int Port { get; set; }
    }
}
