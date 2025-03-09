using HomeBikeServiceAPI.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HomeBikeServiceAPI.Repositories
{
    public interface IMechanicRepository
    {
        Task<IEnumerable<Mechanic>> GetAllMechanicsAsync();
        Task<Mechanic> GetMechanicByIdAsync(int id);
        Task<Mechanic> CreateMechanicAsync(Mechanic mechanic);
        Task<Mechanic> UpdateMechanicAsync(Mechanic mechanic);
        Task<bool> DeleteMechanicAsync(int id);
        Task<Mechanic> GetMechanicByUserIdAsync(int userId);
    }
}
