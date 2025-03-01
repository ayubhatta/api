using HomeBikeServiceAPI.Data;
using HomeBikeServiceAPI.Interfaces;
using HomeBikeServiceAPI.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HomeBikeServiceAPI.Repositories
{
    public class CartRepository : ICartRepository
    {
        private readonly AppDbContext _context;

        public CartRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Cart>> GetCartItemsByUserIdAsync(int userId)
        {
            return await _context.Carts.Where(c => c.UserId == userId).ToListAsync();
        }

        public async Task<Cart> GetDetailAsync(int id)
        {
            return await _context.Carts.FirstOrDefaultAsync(c => c.Id == id);
        }

        // Fetch all carts (Admin only)
        public async Task<IEnumerable<Cart>> GetAllCartsAsync()
        {
            return await _context.Carts.ToListAsync();
        }

        public void Insert(Cart cart)
        {
            _context.Carts.Add(cart);
        }

        public void Update(Cart cart)
        {
            _context.Carts.Update(cart);
        }

        public void Delete(Cart cart)
        {
            _context.Carts.Remove(cart);
        }

        public async Task<bool> SaveAsync()
        {
            return await _context.SaveChangesAsync() > 0;
        }

        // Delete all carts from the table (Admin only)
        public async Task<bool> DeleteAllCarts()
        {
            var carts = await _context.Carts.ToListAsync();
            if (carts.Any())
            {
                _context.Carts.RemoveRange(carts); // Remove all carts
                return await SaveAsync(); // Commit the changes to the database
            }
            return false; // No carts found
        }

        // Delete all carts for a specific user
        public async Task<bool> DeleteAllCartsByUserId(int userId)
        {
            var carts = await _context.Carts.Where(c => c.UserId == userId).ToListAsync();
            if (carts.Any())
            {
                _context.Carts.RemoveRange(carts); // Remove all carts for the user
                return await SaveAsync(); // Commit the changes to the database
            }
            return false; // No carts found for the user
        }
    }
}
