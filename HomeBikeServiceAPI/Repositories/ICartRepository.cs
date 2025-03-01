using HomeBikeServiceAPI.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HomeBikeServiceAPI.Interfaces
{
    public interface ICartRepository
    {
        // Get Cart Items by User ID
        Task<IEnumerable<Cart>> GetCartItemsByUserIdAsync(int userId);

        // Get Cart Detail by Cart ID
        Task<Cart> GetDetailAsync(int id);

        // Fetch all carts (Admin only)
        Task<IEnumerable<Cart>> GetAllCartsAsync();

        // Insert a new cart
        void Insert(Cart cart);

        // Update an existing cart
        void Update(Cart cart);

        // Delete a cart
        void Delete(Cart cart);

        // Save changes to the database
        Task<bool> SaveAsync();

        // Delete all carts from the table (Admin only)
        Task<bool> DeleteAllCarts();

        // Delete all carts for a specific user
        Task<bool> DeleteAllCartsByUserId(int userId);
    }
}
