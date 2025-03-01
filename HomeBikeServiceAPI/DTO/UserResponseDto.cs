using HomeBikeServiceAPI.Models;

namespace HomeBikeServiceAPI.DTO
{
    public class UserResponseDto
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public string Email { get; set; }
        public bool IsAdmin { get; set; }
        public UserType Role { get; set; }
    }
}
