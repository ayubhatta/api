using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using HomeBikeServiceAPI.Data;

[Route("api/[controller]")]
[ApiController]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _context;

    public DashboardController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("total-counts")]
    public async Task<IActionResult> GetTotalCounts()
    {
        try
        {
            var totalUsers = await _context.Users.CountAsync(u => u.Role == HomeBikeServiceAPI.Models.UserType.User); // Filtering users with Role = "User"
            var totalBookings = await _context.Bookings.CountAsync();
            var totalBikeParts = await _context.BikeParts.CountAsync();
            var totalBikeProducts = await _context.BikeProducts.CountAsync();

            var result = new
            {
                Success = true,
                Message = "Data retrieved successfully.",
                TotalCount = new
                {
                    TotalUsers = totalUsers,
                    TotalBookings = totalBookings,
                    TotalBikeParts = totalBikeParts,
                    TotalBikeProducts = totalBikeProducts
                }
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                Success = false,
                Message = "An error occurred while fetching data.",
                Error = ex.Message
            });
        }
    }
}
