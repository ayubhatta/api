using HomeBikeServiceAPI.Models;
using HomeBikeServiceAPI.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RepoBaseModelCore;

namespace HomeBikeServiceAPI.Repositories
{
    public class BookingRepo : _AbsGeneralRepositories<AppDbContext, Booking, int>, IBookingRepo
    {
        public BookingRepo(AppDbContext context) : base(context)
        {

        }

        public async Task<IEnumerable<Booking>> GetAllAsync(int userId)
        {
            return await _entities.Bookings.Where(b => b.UserId == userId).ToListAsync();
        }


        // Delete multiple bookings
        public async Task DeleteRangeAsync(IEnumerable<Booking> bookings)
        {
            _entities.Bookings.RemoveRange(bookings);
            await _entities.SaveChangesAsync();
        }
    }
}
