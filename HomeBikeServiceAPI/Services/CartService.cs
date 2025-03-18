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


        public async Task<bool> UpdateCartItems(List<Cart> carts)
        {
            if (carts == null || !carts.Any()) return false;

            foreach (var cart in carts)
            {
                _cartRepo.Update(cart); // Update each cart item
            }

            return await _cartRepo.SaveAsync(); // Save all updates
        }

        public async Task<bool> UpdatePartQuantities(List<Cart> carts)
        {
            if (carts == null || !carts.Any()) return false;

            foreach (var cart in carts)
            {
                var bikePart = await _context.BikeParts.FindAsync(cart.BikePartsId);
                if (bikePart != null)
                {
                    // Subtract the quantity purchased from the available stock
                    bikePart.Quantity -= cart.Quantity;
                    _context.BikeParts.Update(bikePart); // Update the bike part's quantity
                }
                else
                {
                    return false; // If part not found, return false
                }
            }

            await _context.SaveChangesAsync(); // Commit changes to the database
            return true; // Return true if successful
        }


        public async Task<bool> CompletePaymentAndUpdateQuantity(List<int> cartIds)
        {
            if (cartIds == null || !cartIds.Any())
            {
                throw new ArgumentException("No cart IDs provided."); // Throwing an exception for missing cart IDs
            }

            foreach (var cartId in cartIds)
            {
                // Step 1: Fetch the cart item by ID
                var cartItem = await _context.Carts.FindAsync(cartId);

                if (cartItem == null)
                {
                    throw new KeyNotFoundException($"Cart item with ID {cartId} not found."); // If cart item not found
                }

                // Step 2: Check if payment has already been completed
                if (cartItem.IsPaymentDone)
                {
                    throw new InvalidOperationException($"Payment for cart item {cartId} has already been completed."); // If payment is already done
                }

                // Step 3: Mark the payment as done (set IsPaymentDone to true)
                cartItem.IsPaymentDone = true;

                // Step 4: Update the quantity of the bike part
                var bikePart = await _context.BikeParts.FindAsync(cartItem.BikePartsId);
                if (bikePart != null)
                {
                    // Subtract the quantity from the available stock
                    bikePart.Quantity -= cartItem.Quantity;

                    // Make sure the quantity does not go negative
                    if (bikePart.Quantity < 0)
                    {
                        throw new InvalidOperationException($"Insufficient stock for BikePart {bikePart.Id}. Cannot complete the payment."); // If insufficient stock
                    }

                    _context.BikeParts.Update(bikePart); // Update the bike part
                }
                else
                {
                    throw new KeyNotFoundException($"Bike part with ID {cartItem.BikePartsId} not found."); // If bike part not found
                }

                // Step 5: Update the cart item
                _context.Carts.Update(cartItem);
            }

            // Step 6: Save changes to the database
            await _context.SaveChangesAsync();
            return true;
        }



    }
}
