// Trong Services/IRecommendationService.cs
using MinimartWeb.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MinimartWeb.Services
{
    public interface IRecommendationService
    {
        Task<List<ProductViewModel>> GetRecommendationsAsync(
            int? customerId,
            string? sessionId,
            List<int> initiallyExcludedProductIds,
            int count = 11);
    }
}