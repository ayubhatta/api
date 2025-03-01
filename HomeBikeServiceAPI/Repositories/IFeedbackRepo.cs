using HomeBikeServiceAPI.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HomeBikeServiceAPI.Repositories
{
    public interface IFeedbackRepo
    {
        Task<Feedback> GetFeedbackByIdAsync(int id);
        Task<List<Feedback>> GetAllFeedbacksAsync();
        Task AddFeedbackAsync(Feedback feedback);
        Task UpdateFeedbackAsync(Feedback feedback);
        Task DeleteFeedbackAsync(int id);
    }
}
