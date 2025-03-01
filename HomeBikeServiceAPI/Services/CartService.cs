using HomeBikeServiceAPI.Models;
using HomeBikeServiceAPI.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using HomeBikeServiceAPI.Data;

namespace HomeBikeServiceAPI.Services
{
    public class CartService
    {
        private readonly ICartRepository _cartRepo;
        private readonly AppDbContext _context;

        public CartService(ICartRepository cartRepo, AppDbContext appDbContext)
        {
            _cartRepo = cartRepo;
            _context = appDbContext;
        }

        // Add to Cart
        public async Task<bool> AddToCart(Cart cart)
        {
            if (cart == null) return false;

            _cartRepo.Insert(cart);
            return await _cartRepo.SaveAsync();
        }

        // Get Cart Items by User
        public async Task<IEnumerable<Cart>> GetCartItemsByUser(int userId)
        {
            return await _cartRepo.GetCartItemsByUserIdAsync(userId);
        }

        // Get All Carts (Admin)
        public async Task<IEnumerable<Cart>> GetAllCarts()
        {
            return await _cartRepo.GetAllCartsAsync();
        }

        // Update Cart Item
        public async Task<bool> UpdateCartItem(Cart cart)
        {
            if (cart == null || cart.Id <= 0) return false;

            _cartRepo.Update(cart);
            return await _cartRepo.SaveAsync();
        }

        // Remove Cart Item
        public async Task<bool> RemoveCartItem(int id)
        {
            var cartItem = await _cartRepo.GetDetailAsync(id);
            if (cartItem != null)
            {
                _cartRepo.Delete(cartItem);
                return await _cartRepo.SaveAsync();
            }
            return false;
        }

        // Complete Payment (sets IsPaymentDone to true)
        public async Task<bool> CompletePayment(int id)
        {
            var cartItem = await _cartRepo.GetDetailAsync(id);
            if (cartItem == null) return false;

            cartItem.IsPaymentDone = true;
            _cartRepo.Update(cartItem);
            return await _cartRepo.SaveAsync();
        }

        // Delete specific cart item by Cart ID (User or Admin)
        public async Task<bool> DeleteCartById(int cartId)
        {
            var cartItem = await _cartRepo.GetDetailAsync(cartId);
            if (cartItem != null)
            {
                _cartRepo.Delete(cartItem);
                return await _cartRepo.SaveAsync();
            }
            return false;
        }

        // Delete all carts from the table (Admin only)
        public async Task<bool> DeleteAllCarts()
        {
            var carts = await _cartRepo.GetAllCartsAsync();

            if (carts.Any())
            {
                foreach (var cart in carts)
                {
                    _cartRepo.Delete(cart); // Remove each cart
                }

                return await _cartRepo.SaveAsync(); // Commit the changes to the database
            }

            return false; // No carts found
        }

        // Delete all carts by User ID
        public async Task<bool> DeleteAllCartsByUserId(int userId)
        {
            var carts = await _cartRepo.GetCartItemsByUserIdAsync(userId);

            if (carts.Any())
            {
                foreach (var cart in carts)
                {
                    _cartRepo.Delete(cart); // Remove each cart for the user
                }

                return await _cartRepo.SaveAsync(); // Commit the changes to the database
            }

            return false; // No carts found for the user
        }

        public async Task<bool> DeleteCartItemById(int cartId)
        {
            var cartItem = await _context.Carts.FindAsync(cartId);
            if (cartItem == null)
            {
                return false;
            }

            _context.Carts.Remove(cartItem);
            await _context.SaveChangesAsync();
            return true;
        }

    }
}
