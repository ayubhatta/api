using HomeBikeServiceAPI.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using RepoBaseModelCore;
using HomeBikeServiceAPI.Data;

namespace HomeBikeServiceAPI.Repositories
{
    public interface IBookingRepo: IGeneralRepositories<Booking, int>
    {
        Task<IEnumerable<Booking>> GetAllAsync(int userId);

        Task DeleteRangeAsync(IEnumerable<Booking> bookings);
    }
}
