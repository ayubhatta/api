using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HomeBikeServiceAPI.Models
{
    public enum UserType
    {
        Admin = 0,
        User = 1,
        Mechanic = 2
    }

    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Ensures auto-increment
        public int Id { get; set; }
        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string? ResetPasswordOTP { get; set; }
        public DateTime? ResetPasswordOTPExpiry { get; set; }
        public bool IsAdmin { get; set; }

        [Required]
        public UserType Role { get; set; } = UserType.User; // Default to "User"
    }

    public class LoginRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class ForgotPasswordRequest
    {
        public string Phone { get; set; }
    }

    public class ResetPasswordRequest
    {
        public string Phone { get; set; }
        public string Otp { get; set; }
        public string NewPassword { get; set; }
    }

}
