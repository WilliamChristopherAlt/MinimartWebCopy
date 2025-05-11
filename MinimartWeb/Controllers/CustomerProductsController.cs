using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MinimartWeb.Data;
using MinimartWeb.Model;
using MinimartWeb.Models;       // Cho ErrorViewModel (nếu bạn có)
using MinimartWeb.Services;     // Cho IRecommendationService
using MinimartWeb.ViewModels;   // Cho các ViewModel
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

[AllowAnonymous]
public class CustomerProductsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CustomerProductsController> _logger;
    private readonly IRecommendationService _recommendationService;

    // Cấu hình số lượng sản phẩm cho các section
    private readonly int _productsPerPageOnIndex = 12;
    private readonly int _mainSearchResultsLimit = 100;
    private readonly int _recommendedProductsCountOnIndex = 9;
    private readonly int _otherProductsPageSizeOnIndex = 12;
    private readonly int _otherProductsOnDetailPage = 5;

    // Class PagedProductResult (NÊN chuyển ra ViewModels)
    public class PagedProductResult
    {
        public List<ProductViewModel> Products { get; set; } = new List<ProductViewModel>();
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
    }

    public CustomerProductsController(
        ApplicationDbContext context,
        ILogger<CustomerProductsController> logger,
        IRecommendationService recommendationService)
    {
        _context = context;
        _logger = logger;
        _recommendationService = recommendationService;
    }

    // ACTION INDEX: Xử lý hiển thị sản phẩm, filter, sort, search, recommended, và other products
    public async Task<IActionResult> Index(
        string? sortOrder,
        string? searchString,
        int? categoryId,
        int? supplierId,
        decimal? minPrice,
        decimal? maxPrice,
        int currentPage = 1,
        int otherPage = 1)
    {
        _logger.LogInformation("CustomerProducts/Index called. Filters - Sort: {Sort}, Search: {Search}, Category: {CatId}, Supplier: {SupId}, Price: {MinP}-{MaxP}, Page: {Page}, OtherPage: {OtherP}",
                                sortOrder, searchString, categoryId, supplierId, minPrice, maxPrice, currentPage, otherPage);

        bool isNewQuery = (
            HttpContext.Request.Query.ContainsKey("searchString") || // Có searchString mới (kể cả rỗng để xóa filter search)
            HttpContext.Request.Query.ContainsKey("categoryId") ||
            HttpContext.Request.Query.ContainsKey("supplierId") ||
            HttpContext.Request.Query.ContainsKey("minPrice") ||
            HttpContext.Request.Query.ContainsKey("maxPrice") ||
            HttpContext.Request.Query.ContainsKey("sortOrder") // Thay đổi sort cũng coi như query mới cho phân trang
        ) && !(HttpContext.Request.Query.ContainsKey("currentPage") && HttpContext.Request.Query["currentPage"] != "1") // Không phải đang phân trang Products
          && !(HttpContext.Request.Query.ContainsKey("otherPage") && HttpContext.Request.Query["otherPage"] != "1"); // Không phải đang phân trang OtherProducts

        if (isNewQuery)
        {
            _logger.LogInformation("New filter/search/sort detected in Index. Resetting currentPage and otherPage to 1.");
            currentPage = 1;
            otherPage = 1;
        }

        ViewData["CurrentSort"] = sortOrder;
        ViewData["NameSortParm"] = string.IsNullOrEmpty(sortOrder) || sortOrder == "name_asc" ? "name_desc" : "name_asc";
        ViewData["PriceSortParm"] = sortOrder == "price_asc" ? "price_desc" : "price_asc";
        ViewData["DateSortParm"] = sortOrder == "date_desc" ? "date_asc" : "date_desc";

        var viewModel = new CustomerProductIndexViewModel
        {
            SortOrder = sortOrder,
            SearchString = searchString,
            SelectedCategoryId = categoryId,
            SelectedSupplierId = supplierId,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            CurrentPage = Math.Max(1, currentPage),
            CurrentPageOther = Math.Max(1, otherPage),
            Categories = new SelectList(await _context.Categories.OrderBy(c => c.CategoryName).AsNoTracking().ToListAsync(), "CategoryID", "CategoryName", categoryId),
            Suppliers = new SelectList(await _context.Suppliers.OrderBy(s => s.SupplierName).AsNoTracking().ToListAsync(), "SupplierID", "SupplierName", supplierId)
        };

        try
        {
            IQueryable<ProductType> baseProductsQuery = _context.ProductTypes
                .Where(p => p.IsActive) // Lấy cả những SP có StockAmount = 0 nhưng vẫn active để có thể hiển thị "Hết hàng"
                .Include(p => p.MeasurementUnit)
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .Include(p => p.ProductTags).ThenInclude(pt => pt.Tag)
                .AsNoTracking();

            IQueryable<ProductType> mainProductsQuery = baseProductsQuery.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                mainProductsQuery = mainProductsQuery.Where(p =>
                    EF.Functions.Like(p.ProductName, $"%{searchString}%") ||
                    (p.ProductDescription != null && EF.Functions.Like(p.ProductDescription, $"%{searchString}%")) ||
                    (p.Category != null && EF.Functions.Like(p.Category.CategoryName, $"%{searchString}%")) ||
                    p.ProductTags.Any(pt => EF.Functions.Like(pt.Tag.TagName, $"%{searchString}%"))
                );
            }
            if (categoryId.HasValue && categoryId > 0) mainProductsQuery = mainProductsQuery.Where(p => p.CategoryID == categoryId.Value);
            if (supplierId.HasValue && supplierId > 0) mainProductsQuery = mainProductsQuery.Where(p => p.SupplierID == supplierId.Value);
            if (minPrice.HasValue) mainProductsQuery = mainProductsQuery.Where(p => p.Price >= minPrice.Value);
            if (maxPrice.HasValue && maxPrice > 0) mainProductsQuery = mainProductsQuery.Where(p => p.Price <= maxPrice.Value);

            switch (sortOrder)
            {
                case "name_desc": mainProductsQuery = mainProductsQuery.OrderByDescending(s => s.ProductName); break;
                case "price_asc": mainProductsQuery = mainProductsQuery.OrderBy(s => s.Price); break;
                case "price_desc": mainProductsQuery = mainProductsQuery.OrderByDescending(s => s.Price); break;
                case "date_desc": mainProductsQuery = mainProductsQuery.OrderByDescending(s => s.DateAdded); break;
                case "date_asc": mainProductsQuery = mainProductsQuery.OrderBy(s => s.DateAdded); break;
                default:
                    mainProductsQuery = string.IsNullOrEmpty(sortOrder) || sortOrder == "name_asc"
                                    ? mainProductsQuery.OrderBy(s => s.ProductName)
                                    : mainProductsQuery.OrderByDescending(p => p.DateAdded);
                    if (string.IsNullOrEmpty(sortOrder)) ViewData["NameSortParm"] = "name_asc";
                    break;
            }

            var totalMainProducts = await mainProductsQuery.CountAsync();
            ViewBag.TotalMainProductsCount = totalMainProducts;
            viewModel.TotalPages = (int)Math.Ceiling(totalMainProducts / (double)_productsPerPageOnIndex);
            viewModel.CurrentPage = Math.Max(1, Math.Min(viewModel.CurrentPage, viewModel.TotalPages > 0 ? viewModel.TotalPages : 1));

            var mainProductsEntities = await mainProductsQuery
                .Skip((viewModel.CurrentPage - 1) * _productsPerPageOnIndex)
                .Take(_productsPerPageOnIndex)
                .ToListAsync();

            viewModel.Products = mainProductsEntities
                .Select(p => new ProductViewModel
                {
                    Id = p.ProductTypeID,
                    Name = p.ProductName,
                    ProductDescription = p.ProductDescription,
                    Price = p.Price,
                    ImagePath = p.ImagePath,
                    MeasurementUnitName = p.MeasurementUnit?.UnitName ?? "N/A",
                    StockAmount = p.StockAmount,
                    IsActive = p.IsActive,
                    DateAdded = p.DateAdded
                }).ToList();

            if (!string.IsNullOrWhiteSpace(searchString) && viewModel.CurrentPage == 1 && totalMainProducts > 0)
            {
                int? customerIdSearch = User.Identity?.IsAuthenticated == true ? int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0") : (int?)null;
                if (customerIdSearch == 0) customerIdSearch = null;
                string? sessionIdSearch = HttpContext.Session.GetString("UserSessionId");
                if (customerIdSearch.HasValue || !string.IsNullOrEmpty(sessionIdSearch))
                {
                    try { _context.SearchHistories.Add(new SearchHistory { CustomerID = customerIdSearch, SessionID = customerIdSearch.HasValue ? null : sessionIdSearch, SearchKeyword = searchString, SearchTimestamp = DateTime.UtcNow, NumberOfResults = totalMainProducts }); await _context.SaveChangesAsync(); _logger.LogInformation("Lịch sử tìm kiếm đã lưu (Index). Từ khóa: {Keyword}, Tổng KQ: {Count}", searchString, totalMainProducts); }
                    catch (Exception ex_sh) { _logger.LogError(ex_sh, "Lỗi ghi lịch sử tìm kiếm (Index): {Keyword}", searchString); }
                }
            }

            var allDisplayedProductIds = new HashSet<int>(viewModel.Products.Select(p => p.Id));

            if (mainProductsEntities.Any() && mainProductsEntities.First().Category != null)
            {
                int primaryCategoryId = mainProductsEntities.First().CategoryID;
                viewModel.RecommendationTitle = $"Sản phẩm tương tự trong \"{mainProductsEntities.First().Category.CategoryName}\"";
                viewModel.RecommendedProducts = await baseProductsQuery
                    .Where(p => p.CategoryID == primaryCategoryId && !allDisplayedProductIds.Contains(p.ProductTypeID))
                    .OrderBy(p => Guid.NewGuid())
                    .Take(_recommendedProductsCountOnIndex)
                    .Select(p => new ProductViewModel { Id = p.ProductTypeID, Name = p.ProductName, Price = p.Price, ImagePath = p.ImagePath, MeasurementUnitName = p.MeasurementUnit.UnitName ?? "N/A", StockAmount = p.StockAmount, IsActive = p.IsActive })
                    .ToListAsync();
                viewModel.RecommendedProducts.ForEach(p => allDisplayedProductIds.Add(p.Id));
            }
            if (!viewModel.RecommendedProducts.Any())
            {
                viewModel.RecommendationTitle = "Sản phẩm mới có thể bạn thích";
                viewModel.RecommendedProducts = await baseProductsQuery
                    .Where(p => !allDisplayedProductIds.Contains(p.ProductTypeID))
                    .OrderByDescending(p => p.DateAdded)
                    .Take(_recommendedProductsCountOnIndex)
                    .Select(p => new ProductViewModel { Id = p.ProductTypeID, Name = p.ProductName, Price = p.Price, ImagePath = p.ImagePath, MeasurementUnitName = p.MeasurementUnit.UnitName ?? "N/A", StockAmount = p.StockAmount, IsActive = p.IsActive })
                    .ToListAsync();
                viewModel.RecommendedProducts.ForEach(p => allDisplayedProductIds.Add(p.Id));
            }

            var otherProductsQuery = baseProductsQuery
                .Where(p => !allDisplayedProductIds.Contains(p.ProductTypeID))
                .OrderByDescending(p => Guid.NewGuid());

            var otherProductsResult = await GetPagedProductViewModelsAsync(
                otherProductsQuery,
                _otherProductsPageSizeOnIndex,
                viewModel.CurrentPageOther
            );
            viewModel.OtherProducts = otherProductsResult.Products;
            viewModel.TotalPagesOther = otherProductsResult.TotalPages;
            viewModel.CurrentPageOther = otherProductsResult.CurrentPage;

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi nghiêm trọng trong CustomerProducts/Index khi xử lý dữ liệu.");
            ViewBag.ErrorMessage = "Đã có lỗi xảy ra khi tải trang. Vui lòng thử lại sau.";
            viewModel.Products = new List<ProductViewModel>(); // Đảm bảo Products không null
            viewModel.RecommendedProducts = new List<ProductViewModel>();
            viewModel.OtherProducts = new List<ProductViewModel>();
            return View(viewModel);
        }
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Search(string? keyword)
    {
        _logger.LogInformation("--- CUSTOMERPRODUCTS/SEARCH ACTION CALLED (Redirect Logic) --- Keyword received: {Keyword_Value}", keyword ?? "[[NULL_OR_EMPTY_KEYWORD]]");
        return RedirectToAction(nameof(Index), new
        {
            searchString = keyword,
            currentPage = 1,
            otherPage = 1,
            categoryId = (int?)null,
            supplierId = (int?)null,
            minPrice = (decimal?)null,
            maxPrice = (decimal?)null,
            sortOrder = (string?)null
        });
    }

    private async Task<PagedProductResult> GetPagedProductViewModelsAsync(
       IQueryable<ProductType> sourceQuery,
       int pageSize,
       int currentPage)
    {
        var totalItems = await sourceQuery.CountAsync();
        int calculatedTotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        currentPage = Math.Max(1, Math.Min(currentPage, calculatedTotalPages > 0 ? calculatedTotalPages : 1));

        var products = await sourceQuery
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductViewModel // Đảm bảo ánh xạ ĐỦ các trường ProductViewModel cần
            {
                Id = p.ProductTypeID,
                Name = p.ProductName,
                ProductDescription = p.ProductDescription,
                Price = p.Price,
                ImagePath = p.ImagePath,
                MeasurementUnitName = p.MeasurementUnit != null ? p.MeasurementUnit.UnitName : "N/A",
                StockAmount = p.StockAmount, // Đã có
                IsActive = p.IsActive,       // Đã có
                DateAdded = p.DateAdded
                // TotalUnitsSold, UnitsSoldThisMonth, IsFlashSale nếu bạn có trong ProductViewModel và cần
            })
            .ToListAsync();

        return new PagedProductResult
        {
            Products = products,
            TotalPages = calculatedTotalPages,
            CurrentPage = currentPage
        };
    }

    [AllowAnonymous]
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) { _logger.LogWarning("Details - null ID."); return NotFound(); }
        try
        {
            var productType = await _context.ProductTypes
                .Include(p => p.Category)
                .Include(p => p.MeasurementUnit)
                .Include(p => p.Supplier)
                .Include(p => p.ProductTags).ThenInclude(pt => pt.Tag)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ProductTypeID == id.Value);

            if (productType == null) { _logger.LogWarning("Details - ProductType ID {Id} not found.", id); return NotFound(); }

            int? customerIdView = User.Identity?.IsAuthenticated == true ? int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0") : (int?)null;
            if (customerIdView == 0) customerIdView = null;
            string? sessionIdView = HttpContext.Session.GetString("UserSessionId");
            if (customerIdView.HasValue || !string.IsNullOrEmpty(sessionIdView))
            {
                _context.ViewHistories.Add(new ViewHistory { CustomerID = customerIdView, SessionID = customerIdView.HasValue ? null : sessionIdView, ProductTypeID = productType.ProductTypeID, ViewTimestamp = DateTime.UtcNow });
                await _context.SaveChangesAsync();
                _logger.LogInformation("View history saved for ProductID: {ProdId}, CustID: {CustId}, SessID: {SessId}", productType.ProductTypeID, customerIdView, sessionIdView);
            }

            var productViewModelForDetail = new ProductViewModel
            {
                Id = productType.ProductTypeID,
                Name = productType.ProductName,
                ProductDescription = productType.ProductDescription,
                Price = productType.Price,
                ImagePath = productType.ImagePath,
                StockAmount = productType.StockAmount,
                MeasurementUnitName = productType.MeasurementUnit?.UnitName ?? "N/A",
                IsActive = productType.IsActive,
                DateAdded = productType.DateAdded,
                Tags = productType.ProductTags.Select(pt => pt.Tag.TagName).ToList()
            };

            var excludedFromRecs = new List<int> { productType.ProductTypeID };
            var recommendedProductsVM = await _recommendationService.GetRecommendationsAsync(customerIdView, sessionIdView, excludedFromRecs, 7);

            var allExcludedIdsForDetailPage = new HashSet<int>(excludedFromRecs);
            recommendedProductsVM.ForEach(rp => allExcludedIdsForDetailPage.Add(rp.Id));

            var otherProductsDetailVM = await _context.ProductTypes
                .Where(p => p.IsActive && p.StockAmount > 0 && !allExcludedIdsForDetailPage.Contains(p.ProductTypeID))
                .OrderByDescending(p => p.CategoryID == productType.CategoryID ? 0 : 1)
                .ThenByDescending(p => p.DateAdded)
                .Take(_otherProductsOnDetailPage)
                .Include(p => p.MeasurementUnit)
                .Select(p => new ProductViewModel { Id = p.ProductTypeID, Name = p.ProductName, Price = p.Price, ImagePath = p.ImagePath, MeasurementUnitName = p.MeasurementUnit.UnitName ?? "N/A", StockAmount = p.StockAmount, IsActive = p.IsActive, DateAdded = p.DateAdded })
                .ToListAsync();

            var viewModel = new ProductDetailViewModel
            {
                Product = productType, // Thuộc tính Product trong ProductDetailViewModel là ProductViewModel
                RecommendedProducts = recommendedProductsVM,
                OtherProducts = otherProductsDetailVM
            };
            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Details - Error for ProductID {Id}", id);
            return View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}