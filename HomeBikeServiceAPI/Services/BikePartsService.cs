using HomeBikeServiceAPI.Models;
using HomeBikeServiceAPI.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using HomeBikeServiceAPI.Repositories;
using Microsoft.Extensions.Hosting;

namespace HomeBikeServiceAPI.Services
{
    public class BikePartsService
    {
        private readonly IBikePartsRepository _partsRepo;

        public BikePartsService(IBikePartsRepository bikePartsRepository)
        {
            _partsRepo = bikePartsRepository;
        }

        // Create
        public async Task<bool> CreateBikePart(BikeParts bikePart)
        {
            if (bikePart == null)
                return false;

            _partsRepo.Insert(bikePart);
            return await Task.FromResult(_partsRepo.Save());
        }

        // Read
        public async Task<BikeParts> GetBikePartById(int id)
        {
            if (id <= 0)
                return null;  // Or handle appropriately

            return await Task.FromResult(_partsRepo.GetDetail(id));
        }

        public async Task<IEnumerable<BikeParts>> GetAllBikeParts()
        {
            return await Task.FromResult(_partsRepo.GetList().ToList());
        }

        // Update
        public async Task<bool> UpdateBikePart(BikeParts bikePart)
        {
            if (bikePart == null || bikePart.Id <= 0)
                return false;  // Handle invalid input appropriately

            _partsRepo.Update(bikePart);
            return await Task.FromResult(_partsRepo.Save());
        }

        public async Task<bool> DeleteBikePart(int id)
        {
            var bikePart = await _partsRepo.GetDetailAsync(id);  // Get the bike part by ID.
            if (bikePart != null)
            {
                _partsRepo.Delete(bikePart);  // Delete the bike part.
                bool saveResult = await _partsRepo.SaveAsync();  // Save changes to the database.
                return saveResult;  // Return the result of saving (true if successful, false if not).
            }
            return false;  // If bike part is not found, return false.
        }
    }
}
