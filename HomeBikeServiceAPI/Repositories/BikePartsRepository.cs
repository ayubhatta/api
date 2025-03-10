using HomeBikeServiceAPI.Models;
using HomeBikeServiceAPI.Interfaces;
using RepoBaseModelCore;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using HomeBikeServiceAPI.Data;

namespace HomeBikeServiceAPI.Repositories
{
    public class BikePartsRepository : _AbsGeneralRepositories<AppDbContext, BikeParts, int>, IBikePartsRepository
    {
        public BikePartsRepository(AppDbContext context) : base(context) { }

        // Example of an additional custom method to get a BikePart by its PartName
        public async Task<BikeParts> GetByPartNameAsync(string partName)
        {
            return await _Query.FirstOrDefaultAsync(x => x.PartName == partName);
        }

        // You can add any additional methods or overrides if needed

        public void Delete(BikeParts bikePart)
        {
            _entities.BikeParts.Remove(bikePart);  // Mark the entity as deleted.
        }


    }
}
