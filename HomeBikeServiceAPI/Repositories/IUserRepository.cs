using HomeBikeServiceAPI.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HomeBikeServiceAPI.Repositories
{
    public interface IUserRepository
    {
        Task<User> GetUserByEmailAsync(string email);
        Task<User> GetUserByIdAsync(int userId);
        Task<User> GetUserByPhoneAsync(string phone);
        Task<User> CreateUserAsync(User user);
        Task<User> UpdateUserAsync(User user);
        Task<List<User>> GetAllUsersAsync();
        void DeleteUser(User user);
        Task<bool> SaveChangesAsync();
    }
}
