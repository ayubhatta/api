using HomeBikeServiceAPI.Models;
using RepoBaseModelCore;
using System.Threading.Tasks;

namespace HomeBikeServiceAPI.Interfaces
{
    public interface IBikePartsRepository : IGeneralRepositories<BikeParts, int>
    {
        void Delete(BikeParts bikePart);
        Task<BikeParts> GetByPartNameAsync(string partName);
    }
}
