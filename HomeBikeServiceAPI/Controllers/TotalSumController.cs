using HomeBikeServiceAPI.Services;
using HomeBikeServiceAPI.Repositories;
using Microsoft.AspNetCore.Mvc;
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


        [HttpGet("{bookingId}")]
        public async Task<IActionResult> GetTotalAmount(int bookingId)
        {
            try
            {
                // Get the booking record based on BookingId
                var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId);
                if (booking == null)
                {
                    return NotFound(new { success = false, message = "Booking not found." });
                }

                // Get the bike product based on the BikeId in the Booking table
                var bikeProduct = await _context.BikeProducts.FirstOrDefaultAsync(bp => bp.Id == booking.BikeId);
                if (bikeProduct == null)
                {
                    return NotFound(new { success = false, message = "Bike product not found." });
                }

                // Total amount from the BikePrice field
                decimal totalAmount = bikeProduct.BikePrice;

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
