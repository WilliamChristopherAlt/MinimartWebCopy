// Trong Controllers/CustomerProductsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MinimartWeb.Data;
using MinimartWeb.Model;
using MinimartWeb.Models;       // Cho ErrorViewModel (nếu có)
using MinimartWeb.Services;     // Namespace của IRecommendationService
using MinimartWeb.ViewModels;   // Namespace của ProductDetailViewModel, ProductViewModel, SearchResultsViewModel
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

public class CustomerProductsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CustomerProductsController> _logger;
    private readonly IRecommendationService _recommendationService; // Inject Service
    private readonly int _otherProductsOnDetailPage = 5; // Số "Sản phẩm khác" hiển thị trên trang chi tiết
    private readonly int _otherProductsOnSearchPage = 15; // Số "Sản phẩm khác" trên trang tìm kiếm

    public CustomerProductsController(
        ApplicationDbContext context,
        ILogger<CustomerProductsController> logger,
        IRecommendationService recommendationService) // Thêm IRecommendationService vào constructor
    {
        _context = context;
        _logger = logger;
        _recommendationService = recommendationService; // Gán service
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        try
        {
            // Tạo ProductViewModel trực tiếp từ query
            var products = await _context.ProductTypes
                .Where(p => p.IsActive && p.StockAmount > 0)
                .Include(p => p.MeasurementUnit) // Cần cho MeasurementUnitName
                                                 //.Include(p => p.Category) // Include nếu cần cho card trên trang Index này
                                                 //.Include(p => p.Supplier) // Include nếu cần
                .AsNoTracking()
                .OrderByDescending(p => p.DateAdded)
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
                    DateAdded = p.DateAdded,
                    // OriginalPrice = p.OriginalPrice, // Nếu bạn có
                    // TotalUnitsSold và UnitsSoldThisMonth có thể không cần cho trang Index chung này
                    // hoặc bạn có thể tính toán nếu muốn (sẽ làm query nặng hơn)
                })
                .ToListAsync();

            // Nếu trang Index này sử dụng một ViewModel riêng (ví dụ: CustomerProductIndexViewModel)
            // thì bạn sẽ tạo và truyền ViewModel đó vào View("Index", viewModel);
            return View(products); // Hiện tại đang truyền List<ProductViewModel>
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving products for CustomerProducts/Index.");
            return View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }

    [AllowAnonymous]
    public async Task<IActionResult> Search(string keyword, int currentPage = 1)
    {
        _logger.LogInformation("Search action called with keyword: '{Keyword}', CurrentPage for Others: {CurrentPage}", keyword, currentPage);
        var viewModel = new SearchResultsViewModel
        {
            Keyword = keyword ?? string.Empty,
            CurrentPage = currentPage // Dùng cho phân trang "Sản phẩm khác" trên trang search
        };

        try
        {
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var searchResultsQuery = _context.ProductTypes
                    .Where(p => p.IsActive &&
                                (p.ProductName.Contains(keyword) ||
                                 (p.ProductDescription != null && p.ProductDescription.Contains(keyword))))
                    .Include(p => p.MeasurementUnit)
                    .Include(p => p.Category)
                    .AsNoTracking();

                viewModel.SearchResults = await searchResultsQuery
                    .Select(p => new ProductViewModel
                    { /* ... gán các thuộc tính ... */
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
                    .OrderByDescending(p => p.DateAdded)
                    .ToListAsync();
                _logger.LogInformation("Found {Count} products for keyword '{Keyword}'.", viewModel.SearchResults.Count, keyword);

                // Ghi lịch sử tìm kiếm
                int? customerIdSearch = HttpContext.User.Identity?.IsAuthenticated == true ?
                    (await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Username == User.Identity.Name))?.CustomerID : null;
                string? sessionIdSearch = HttpContext.Session.GetString("UserSessionId");
                if (customerIdSearch.HasValue || !string.IsNullOrEmpty(sessionIdSearch))
                {
                    _context.SearchHistories.Add(new SearchHistory
                    {
                        CustomerID = customerIdSearch,
                        SessionID = customerIdSearch.HasValue ? null : sessionIdSearch,
                        SearchKeyword = keyword,
                        SearchTimestamp = DateTime.Now,
                        NumberOfResults = viewModel.SearchResults.Count
                    });
                    await _context.SaveChangesAsync();
                }
            }
            else { _logger.LogInformation("Search keyword is empty."); }

            var searchedProductIds = viewModel.SearchResults.Select(sr => sr.Id).ToHashSet();
            var otherProductsQuery = _context.ProductTypes
                                    .Where(p => p.IsActive && !searchedProductIds.Contains(p.ProductTypeID))
                                    .Include(p => p.MeasurementUnit)
                                    .OrderByDescending(p => p.DateAdded)
                                    .AsNoTracking();

            var totalOtherProducts = await otherProductsQuery.CountAsync();
            viewModel.TotalPages = (int)Math.Ceiling(totalOtherProducts / (double)_otherProductsOnSearchPage);
            if (viewModel.CurrentPage < 1) viewModel.CurrentPage = 1;
            if (viewModel.CurrentPage > viewModel.TotalPages && viewModel.TotalPages > 0) viewModel.CurrentPage = viewModel.TotalPages;

            viewModel.OtherProducts = await otherProductsQuery
                                        .Skip((viewModel.CurrentPage - 1) * _otherProductsOnSearchPage)
                                        .Take(_otherProductsOnSearchPage)
                                        .Select(p => new ProductViewModel
                                        { /* ... gán các thuộc tính ... */
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
            _logger.LogInformation("Fetched {Count} other products for search page {Page}.", viewModel.OtherProducts.Count, viewModel.CurrentPage);
        }
        catch (Exception ex) { _logger.LogError(ex, "Error in Search action for keyword: {Keyword}", keyword); }
        return View("SearchResults", viewModel);
    }

    [AllowAnonymous]
    public async Task<IActionResult> Details(int? id, int currentPageOther = 1) // currentPageOther cho "Sản phẩm khác" trên trang details
    {
        if (id == null) { _logger.LogWarning("Details action called with null ProductTypeID."); return NotFound(); }
        _logger.LogInformation("Details action called for ProductTypeID: {ProductTypeId}, OtherProductsPage: {CurrentPageOther}", id, currentPageOther);

        try
        {
            var productType = await _context.ProductTypes
                .Include(p => p.Category)
                .Include(p => p.MeasurementUnit)
                .Include(p => p.Supplier)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ProductTypeID == id.Value);

            if (productType == null) { _logger.LogWarning("ProductType with ID {ProductTypeId} not found.", id); return NotFound(); }

            // --- GHI LỊCH SỬ XEM ---
            try
            {
                int? customerIdView = HttpContext.User.Identity?.IsAuthenticated == true ?
                    (await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Username == User.Identity.Name))?.CustomerID : null;
                string? sessionIdView = HttpContext.Session.GetString("UserSessionId");

                if ((customerIdView.HasValue || !string.IsNullOrEmpty(sessionIdView)))
                {
                    var viewHistoryEntry = new ViewHistory { CustomerID = customerIdView, SessionID = customerIdView.HasValue ? null : sessionIdView, ProductTypeID = productType.ProductTypeID, ViewTimestamp = DateTime.Now };
                    _context.ViewHistories.Add(viewHistoryEntry);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("View history saved for ProductID: {ProductId}, User/Session: {UserSession}", productType.ProductTypeID, customerIdView ?? (object)sessionIdView ?? "Unknown");
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "Error saving view history for ProductID {ProductId}", id); }


            // --- LẤY SẢN PHẨM GỢI Ý ---
            int? customerIdRec = HttpContext.User.Identity?.IsAuthenticated == true ?
                (await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Username == User.Identity.Name))?.CustomerID : null;
            string? sessionIdRec = HttpContext.Session.GetString("UserSessionId");
            var excludedFromRecs = new List<int> { productType.ProductTypeID }; // Loại trừ sản phẩm đang xem
            var recommendedProducts = await _recommendationService.GetRecommendationsAsync(customerIdRec, sessionIdRec, excludedFromRecs, 11); // Lấy 7 gợi ý


            // --- LẤY "SẢN PHẨM KHÁC" (CÙNG LOẠI HOẶC MỚI NHẤT, CÓ PHÂN TRANG) ---
            var excludedFromOthers = new HashSet<int>(excludedFromRecs);
            foreach (var recP in recommendedProducts) { excludedFromOthers.Add(recP.Id); }

            IQueryable<ProductType> otherProductsBaseQuery = _context.ProductTypes
                                    .Where(p => p.IsActive && !excludedFromOthers.Contains(p.ProductTypeID)); // Luôn loại trừ SP đang xem và SP gợi ý

            // Ưu tiên cùng category
            var sameCategoryQuery = otherProductsBaseQuery.Where(p => p.CategoryID == productType.CategoryID);
            var totalSameCategory = await sameCategoryQuery.CountAsync();

            IQueryable<ProductType> finalOtherProductsQuery;
            if (totalSameCategory >= _otherProductsOnDetailPage)
            { // Nếu đủ SP cùng loại cho ít nhất 1 trang
                finalOtherProductsQuery = sameCategoryQuery.OrderByDescending(p => p.DateAdded);
            }
            else
            { // Không đủ SP cùng loại, lấy SP mới nhất (vẫn loại trừ những cái đã có)
                finalOtherProductsQuery = otherProductsBaseQuery.OrderByDescending(p => p.DateAdded);
            }
            finalOtherProductsQuery = finalOtherProductsQuery.Include(p => p.MeasurementUnit); // Include cần thiết


            var totalOtherProducts = await finalOtherProductsQuery.CountAsync();
            var totalPagesOther = (int)Math.Ceiling(totalOtherProducts / (double)_otherProductsOnDetailPage);
            if (currentPageOther < 1) currentPageOther = 1;
            if (currentPageOther > totalPagesOther && totalPagesOther > 0) currentPageOther = totalPagesOther;

            var otherProductsVM = await finalOtherProductsQuery
                                    .Skip((currentPageOther - 1) * _otherProductsOnDetailPage)
                                    .Take(_otherProductsOnDetailPage)
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
            _logger.LogInformation("Fetched {Count} other products for details page {Page}.", otherProductsVM.Count, currentPageOther);


            var viewModel = new ProductDetailViewModel
            {
                Product = productType,
                RecommendedProducts = recommendedProducts,
                OtherProducts = otherProductsVM,
                CurrentPageOther = currentPageOther,
                TotalPagesOther = totalPagesOther
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving details page for ProductType ID: {Id}", id);
            return View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}