// Trong Services/RecommendationService.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MinimartWeb.Data;
using MinimartWeb.Model;
using MinimartWeb.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions; // Cho PredicateBuilder
using System.Threading.Tasks;

namespace MinimartWeb.Services
{
    public class RecommendationService : IRecommendationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RecommendationService> _logger;
        private const int SIMILAR_PRODUCTS_FETCH_MULTIPLIER = 3; // Lấy gấp X lần số lượng cần để có nhiều lựa chọn hơn

        public RecommendationService(ApplicationDbContext context, ILogger<RecommendationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<ProductViewModel>> GetRecommendationsAsync(int? customerId, string? sessionId, List<int> initiallyExcludedProductIds, int targetCount = 7)
        {
            var recommendations = new List<ProductViewModel>();
            var excludedProductIds = new HashSet<int>(initiallyExcludedProductIds ?? new List<int>());

            _logger.LogInformation("[REC_SVC] Starting recommendations for CustomerID: {CustomerId}, SessionID: {SessionId}. Excluding {ExcludedCount} initial IDs. Target count: {TargetCount}",
                customerId, sessionId, excludedProductIds.Count, targetCount);

            // --- Xác định hành động gần nhất ---
            var lastView = await GetLastActionAsync<ViewHistory>(
                vh => customerId.HasValue ? vh.CustomerID == customerId : (sessionId != null && vh.SessionID == sessionId),
                vh => vh.ViewTimestamp
            );
            var lastSearch = await GetLastActionAsync<SearchHistory>(
                sh => customerId.HasValue ? sh.CustomerID == customerId : (sessionId != null && sh.SessionID == sessionId),
                sh => sh.SearchTimestamp
            );
            // Lịch sử mua hàng có thể ít tức thời hơn cho "gợi ý ngay", nhưng vẫn có thể dùng làm nguồn
            var lastPurchase = customerId.HasValue ? await GetLastPurchaseActionAsync(customerId.Value) : null;


            // --- Ưu tiên gợi ý dựa trên hành động gần nhất ---
            if (lastView != null && (lastSearch == null || lastView.ViewTimestamp >= lastSearch.SearchTimestamp) && (lastPurchase == null || lastView.ViewTimestamp >= lastPurchase.SaleDate))
            {
                _logger.LogInformation("[REC_SVC] Context: Last View (ProductID: {ProductId})", lastView.ProductTypeID);
                if (lastView.ProductTypeID != 0)
                {
                    excludedProductIds.Add(lastView.ProductTypeID); // Không gợi ý lại chính SP vừa xem
                    var similarToViewed = await GetSimilarProductsByProductAsync(lastView.ProductTypeID, targetCount, excludedProductIds);
                    recommendations.AddRange(similarToViewed);
                    foreach (var p in similarToViewed) excludedProductIds.Add(p.Id);
                }
            }
            else if (lastSearch != null && (lastPurchase == null || lastSearch.SearchTimestamp >= lastPurchase.SaleDate))
            {
                _logger.LogInformation("[REC_SVC] Context: Last Search (Keyword: '{Keyword}')", lastSearch.SearchKeyword);
                var fromSearch = await GetProductsByKeywordAsync(lastSearch.SearchKeyword, targetCount - recommendations.Count, excludedProductIds);
                recommendations.AddRange(fromSearch);
                foreach (var p in fromSearch) excludedProductIds.Add(p.Id);
            }
            else if (lastPurchase != null)
            {
                _logger.LogInformation("[REC_SVC] Context: Last Purchase (SaleID: {SaleId})", lastPurchase.SaleID);
                var purchasedProductIdsInLastSale = await _context.SaleDetails
                    .Where(sd => sd.SaleID == lastPurchase.SaleID)
                    .Select(sd => sd.ProductTypeID).Distinct().ToListAsync();

                excludedProductIds.UnionWith(purchasedProductIdsInLastSale);

                if (purchasedProductIdsInLastSale.Any())
                {
                    // Lấy ngẫu nhiên 1 sản phẩm trong đơn hàng cuối để tìm sp tương tự
                    // Hoặc có thể lặp qua từng sản phẩm và tổng hợp kết quả
                    var randomProductIdFromPurchase = purchasedProductIdsInLastSale.OrderBy(x => Guid.NewGuid()).FirstOrDefault();
                    if (randomProductIdFromPurchase != 0)
                    {
                        var similarToPurchased = await GetSimilarProductsByProductAsync(randomProductIdFromPurchase, targetCount - recommendations.Count, excludedProductIds);
                        recommendations.AddRange(similarToPurchased);
                        foreach (var p in similarToPurchased) excludedProductIds.Add(p.Id);
                    }
                }
            }
            _logger.LogInformation("[REC_SVC] Found {Count} recommendations from contextual actions.", recommendations.Count);


            // ----- DỰ PHÒNG: Nếu vẫn không đủ, lấy thêm sản phẩm phổ biến / mới nhất -----
            if (recommendations.Count < targetCount)
            {
                _logger.LogInformation("[REC_SVC] Fallback: Not enough ({Current}/{Target}), fetching popular.", recommendations.Count, targetCount);
                var popularFallback = await _context.SaleDetails
                    .GroupBy(sd => sd.ProductTypeID)
                    .Select(g => new { ProductTypeId = g.Key, TotalSold = g.Sum(x => x.Quantity) })
                    .OrderByDescending(g => g.TotalSold)
                    .Where(g => _context.ProductTypes.Any(pt => pt.ProductTypeID == g.ProductTypeId && pt.IsActive) && !excludedProductIds.Contains(g.ProductTypeId))
                    .Select(g => g.ProductTypeId)
                    .Take(targetCount - recommendations.Count + 5)
                    .ToListAsync();
                if (popularFallback.Any())
                {
                    var popularProducts = await GetProductViewModelsByIds(popularFallback.Take(targetCount - recommendations.Count).ToList());
                    recommendations.AddRange(popularProducts);
                    _logger.LogInformation("[REC_SVC] Fallback: Added {Count} popular products.", popularProducts.Count);
                    foreach (var p in popularProducts) excludedProductIds.Add(p.Id);
                }
            }
            if (recommendations.Count < targetCount)
            {
                _logger.LogInformation("[REC_SVC] Fallback: Still not enough ({Current}/{Target}), fetching latest.", recommendations.Count, targetCount);
                var latestProducts = await _context.ProductTypes
                    .Where(pt => pt.IsActive && !excludedProductIds.Contains(pt.ProductTypeID))
                    .OrderByDescending(pt => pt.DateAdded)
                    .Take(targetCount - recommendations.Count)
                    .SelectToProductViewModel()
                    .ToListAsync();
                recommendations.AddRange(latestProducts);
                _logger.LogInformation("[REC_SVC] Fallback: Added {Count} latest products.", latestProducts.Count);
            }

            return recommendations.DistinctBy(p => p.Id).Take(targetCount).ToList();
        }

        // ----- HÀM HELPER CHUNG LẤY HÀNH ĐỘNG GẦN NHẤT -----
        private async Task<T?> GetLastActionAsync<T>(Expression<Func<T, bool>> predicate, Expression<Func<T, DateTime>> orderBySelector) where T : class
        {
            return await _context.Set<T>().Where(predicate).OrderByDescending(orderBySelector).FirstOrDefaultAsync();
        }
        private async Task<Sale?> GetLastPurchaseActionAsync(int customerId)
        {
            return await _context.Sales
                .Where(s => s.CustomerID == customerId)
                .OrderByDescending(s => s.SaleDate)
                .FirstOrDefaultAsync();
        }


        // ----- HÀM HELPER LẤY SẢN PHẨM TƯƠNG TỰ -----
        private async Task<List<ProductViewModel>> GetSimilarProductsByProductAsync(int sourceProductTypeId, int takeCount, HashSet<int> currentExcludedIds)
        {
            if (takeCount <= 0) return new List<ProductViewModel>();

            var sourceProduct = await _context.ProductTypes
                                    .AsNoTracking()
                                    .Include(p => p.Category) // Cần Category
                                    .Include(p => p.ProductTags) // Cần Tags
                                        .ThenInclude(ptg => ptg.Tag)
                                    .FirstOrDefaultAsync(pt => pt.ProductTypeID == sourceProductTypeId);

            if (sourceProduct == null) return new List<ProductViewModel>();

            _logger.LogInformation("[REC_SVC_SIMILAR] Source for similarity: ID {SourceId}, CategoryID {CatId}", sourceProductTypeId, sourceProduct.CategoryID);

            var similarProducts = new List<ProductViewModel>();
            // Tạo một HashSet mới cho riêng hàm này để không ảnh hưởng excludedIds của hàm cha
            var exclusionsForThisScope = new HashSet<int>(currentExcludedIds);
            exclusionsForThisScope.Add(sourceProductTypeId); // Luôn loại trừ chính nó

            // 1. Cùng Category
            if (similarProducts.Count < takeCount)
            {
                var byCategory = await _context.ProductTypes
                    .Where(pt => pt.IsActive &&
                                 pt.CategoryID == sourceProduct.CategoryID &&
                                 !exclusionsForThisScope.Contains(pt.ProductTypeID))
                    .Include(pt => pt.MeasurementUnit)
                    .OrderBy(x => Guid.NewGuid()) // Hoặc sắp xếp theo tiêu chí khác (ví dụ: bán chạy trong category)
                    .Take(takeCount * SIMILAR_PRODUCTS_FETCH_MULTIPLIER) // Lấy nhiều hơn số lượng cần
                    .SelectToProductViewModel()
                    .ToListAsync();

                similarProducts.AddRange(byCategory);
                foreach (var p in byCategory) exclusionsForThisScope.Add(p.Id); // Thêm vào loại trừ để bước sau không lấy lại
                _logger.LogInformation("[REC_SVC_SIMILAR] ByCategory for {SourceId}: Found {Count}, Added to recs: {AddedCount}", sourceProductTypeId, byCategory.Count, similarProducts.Count);
            }


            // 2. Cùng Tags (nếu vẫn chưa đủ)
            if (similarProducts.Count < takeCount && sourceProduct.ProductTags.Any())
            {
                var sourceTagIds = sourceProduct.ProductTags.Select(ptg => ptg.TagID).ToList();
                _logger.LogInformation("[REC_SVC_SIMILAR] Source tags for {SourceId}: [{TagIds}]", sourceProductTypeId, string.Join(",", sourceTagIds));

                var byTags = await _context.ProductTypes
                    .Where(pt => pt.IsActive &&
                                 !exclusionsForThisScope.Contains(pt.ProductTypeID) && // Đã bao gồm source và những SP từ category
                                 pt.ProductTags.Any(ptg => sourceTagIds.Contains(ptg.TagID))) // SP có ít nhất 1 tag chung
                    .Include(pt => pt.MeasurementUnit)
                    .OrderBy(x => Guid.NewGuid())
                    .Take(takeCount * SIMILAR_PRODUCTS_FETCH_MULTIPLIER - similarProducts.Count) // Lấy số lượng còn thiếu
                    .SelectToProductViewModel()
                    .ToListAsync();

                similarProducts.AddRange(byTags);
                _logger.LogInformation("[REC_SVC_SIMILAR] ByTags for {SourceId}: Found {Count}, Total recs now: {TotalCount}", sourceProductTypeId, byTags.Count, similarProducts.Count);
            }
            return similarProducts.DistinctBy(p => p.Id).Take(takeCount).ToList();
        }

        // ----- HÀM HELPER LẤY SẢN PHẨM THEO TỪ KHÓA -----
        private async Task<List<ProductViewModel>> GetProductsByKeywordAsync(string keyword, int takeCount, HashSet<int> excludedProductIds)
        {
            // ... Logic của GetProductsByKeywordAsync giữ nguyên như phiên bản trước, chỉ sửa Log ...
            if (takeCount <= 0 || string.IsNullOrWhiteSpace(keyword)) return new List<ProductViewModel>();
            _logger.LogInformation("[REC_SVC_KEYWORD] Getting by keyword: '{Keyword}', Max: {TakeCount}", keyword, takeCount);
            var tempKeyword = keyword.ToLower();
            var predicate = PredicateBuilder.False<ProductType>().Or(p => p.ProductName.ToLower().Contains(tempKeyword) || (p.ProductDescription != null && p.ProductDescription.ToLower().Contains(tempKeyword)));
            return await _context.ProductTypes
                .Where(pt => pt.IsActive && !excludedProductIds.Contains(pt.ProductTypeID))
                .Where(predicate)
                .Include(pt => pt.MeasurementUnit)
                .OrderByDescending(pt => pt.DateAdded)
                .Take(takeCount)
                .SelectToProductViewModel()
                .ToListAsync();
        }

        // Hàm helper lấy ProductViewModel từ danh sách ID
        private async Task<List<ProductViewModel>> GetProductViewModelsByIds(List<int> productIds)
        {
            if (!productIds.Any()) return new List<ProductViewModel>();
            return await _context.ProductTypes
                .Where(pt => pt.IsActive && productIds.Contains(pt.ProductTypeID))
                .Include(pt => pt.MeasurementUnit)
                .Select(p => new ProductViewModel
                {
                    Id = p.ProductTypeID,
                    Name = p.ProductName,
                    ProductDescription = p.ProductDescription,
                    Price = p.Price,
                    ImagePath = p.ImagePath,
                    MeasurementUnitName = p.MeasurementUnit.UnitName ?? "N/A",
                    StockAmount = p.StockAmount,
                    IsActive = p.IsActive,
                    DateAdded = p.DateAdded
                })
                .ToListAsync();
        }
    }

    // Lớp PredicateBuilder
    public static class PredicateBuilder { public static Expression<Func<T, bool>> True<T>() { return f => true; } public static Expression<Func<T, bool>> False<T>() { return f => false; } public static Expression<Func<T, bool>> Or<T>(this Expression<Func<T, bool>> expr1, Expression<Func<T, bool>> expr2) { var invokedExpr = Expression.Invoke(expr2, expr1.Parameters.Cast<Expression>()); return Expression.Lambda<Func<T, bool>>(Expression.OrElse(expr1.Body, invokedExpr), expr1.Parameters); } public static Expression<Func<T, bool>> And<T>(this Expression<Func<T, bool>> expr1, Expression<Func<T, bool>> expr2) { var invokedExpr = Expression.Invoke(expr2, expr1.Parameters.Cast<Expression>()); return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(expr1.Body, invokedExpr), expr1.Parameters); } }
    // Lớp Extension Method cho Select
    public static class ProductTypeQueryExtensions
    {
        public static IQueryable<ProductViewModel> SelectToProductViewModel(this IQueryable<ProductType> query)
        {
            return query.Select(p => new ProductViewModel
            {
                Id = p.ProductTypeID,
                Name = p.ProductName,
                ProductDescription = p.ProductDescription,
                Price = p.Price,
                ImagePath = p.ImagePath,
                MeasurementUnitName = p.MeasurementUnit.UnitName ?? "N/A", // Cần Include MeasurementUnit trước đó
                StockAmount = p.StockAmount,
                IsActive = p.IsActive,
                DateAdded = p.DateAdded
            });
        }
    }
}