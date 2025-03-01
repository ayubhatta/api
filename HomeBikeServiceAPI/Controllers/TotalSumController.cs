using HomeBikeServiceAPI.Services;
using HomeBikeServiceAPI.Repositories;
using HomeBikeServiceAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using HomeBikeServiceAPI.Data;

namespace HomeBikeServiceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TotalSumController : ControllerBase
    {
        private readonly CartService _cartService;
        private readonly IBookingRepo _bookingRepo;
        private readonly AppDbContext _context;
        private readonly ILogger<TotalSumController> _logger;

        public TotalSumController(CartService cartService, IBookingRepo bookingRepo, AppDbContext context, ILogger<TotalSumController> logger)
        {
            _cartService = cartService;
            _bookingRepo = bookingRepo;
            _context = context;
            _logger = logger;
        }


        // Get Total Amount (Bike Parts from Cart + Booked Bike Products from BikeProducts table)
        [HttpGet("{userId}")]
        public async Task<IActionResult> GetTotalAmount(int userId)
        {
            try
            {
                // Check if the user exists
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "User not found." });
                }

                // Check if the user is of role "User"
                if (user.Role != UserType.User)  // Compare with the enum value
                {
                    return Unauthorized(new { success = false, message = "Access denied: User is not of type 'User'." });
                }


                // Get total price of bike parts added to cart
                var cartItems = await _cartService.GetCartItemsByUser(userId);
                var cartTotal = cartItems?.Sum(c => c.BikeParts?.Price) ?? 0;

                // Get total price of booked bike products from the BikeProducts table
                var bookings = await _bookingRepo.GetAllAsync(userId);
                decimal bookedProductsTotal = 0;

                foreach (var booking in bookings)
                {
                    // Get the bike product based on the BikeId in the Booking table
                    var bikeProduct = await _context.BikeProducts.FirstOrDefaultAsync(bp => bp.Id == booking.BikeId);

                    if (bikeProduct != null)
                    {
                        bookedProductsTotal += bikeProduct.BikePrice; // Add the bike product price to the total
                    }
                }

                // Calculate the total amount
                var totalAmount = cartTotal + bookedProductsTotal;

                return Ok(new
                {
                    success = true,
                    message = "Total amount calculated successfully.",
                    TotalAmount = totalAmount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating total amount.");
                return StatusCode(500, new { success = false, message = "Internal server error.", error = ex.Message });
            }
        }
    }
}
