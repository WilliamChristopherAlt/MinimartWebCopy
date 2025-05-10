// Trong Controllers/CustomerProductsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MinimartWeb.Data;
using MinimartWeb.Model;
using MinimartWeb.Models;       // Cho ErrorViewModel
using MinimartWeb.Services;
using MinimartWeb.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

[AllowAnonymous]
public class CustomerProductsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CustomerProductsController> _logger;
    private readonly IRecommendationService _recommendationService;
    private readonly int _productsPerPage = 12;
    private readonly int _otherProductsOnDetailPage = 5;

    public CustomerProductsController(
        ApplicationDbContext context,
        ILogger<CustomerProductsController> logger,
        IRecommendationService recommendationService)
    {
        _context = context;
        _logger = logger;
        _recommendationService = recommendationService;
    }

    public async Task<IActionResult> Index(
        string? sortOrder,
        string? searchString,
        int? categoryId,
        int? supplierId,
        decimal? minPrice,
        decimal? maxPrice,
        int currentPage = 1)
    {
        _logger.LogInformation("CustomerProducts/Index called. Filters - Sort: {Sort}, Search: {Search}, Category: {CatId}, Supplier: {SupId}, Price: {MinP}-{MaxP}, Page: {Page}",
                                sortOrder, searchString, categoryId, supplierId, minPrice, maxPrice, currentPage);

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
            CurrentPage = currentPage
        };

        try
        {
            IQueryable<ProductType> productsQuery = _context.ProductTypes
                .Where(p => p.IsActive && p.StockAmount > 0)
                .Include(p => p.Category).Include(p => p.Supplier).Include(p => p.MeasurementUnit)
                .AsNoTracking();

            int? customerIdForSearchHistory = null;
            if (User.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(User.Identity.Name))
            {
                var customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Username == User.Identity.Name);
                if (customer != null) customerIdForSearchHistory = customer.CustomerID;
            }
            string? sessionIdForSearchHistory = HttpContext.Session.GetString("UserSessionId");

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                string searchLower = searchString.ToLower();
                productsQuery = productsQuery.Where(p => p.ProductName.ToLower().Contains(searchLower) ||
                                                       (p.ProductDescription != null && p.ProductDescription.ToLower().Contains(searchLower)));

                if (customerIdForSearchHistory.HasValue || !string.IsNullOrEmpty(sessionIdForSearchHistory))
                {
                    // Số lượng kết quả sẽ được cập nhật sau khi query chính được thực thi
                    // Chỉ thêm bản ghi search, NumberOfResults sẽ được cập nhật sau khi có kết quả cuối cùng của query product
                }
            }
            if (categoryId.HasValue && categoryId > 0) productsQuery = productsQuery.Where(p => p.CategoryID == categoryId.Value);
            if (supplierId.HasValue && supplierId > 0) productsQuery = productsQuery.Where(p => p.SupplierID == supplierId.Value);
            if (minPrice.HasValue) productsQuery = productsQuery.Where(p => p.Price >= minPrice.Value);
            if (maxPrice.HasValue && maxPrice > 0) productsQuery = productsQuery.Where(p => p.Price <= maxPrice.Value);

            switch (sortOrder)
            {
                case "name_desc": productsQuery = productsQuery.OrderByDescending(s => s.ProductName); break;
                case "price_asc": productsQuery = productsQuery.OrderBy(s => s.Price); break;
                case "price_desc": productsQuery = productsQuery.OrderByDescending(s => s.Price); break;
                case "date_desc": productsQuery = productsQuery.OrderByDescending(s => s.DateAdded); break;
                case "date_asc": productsQuery = productsQuery.OrderBy(s => s.DateAdded); break;
                default: productsQuery = string.IsNullOrEmpty(sortOrder) ? productsQuery.OrderByDescending(p => p.DateAdded) : productsQuery.OrderBy(s => s.ProductName); break; // Mặc định sắp xếp theo mới nhất nếu không có sortOrder
            }

            var totalProducts = await productsQuery.CountAsync(); // Đếm sau khi lọc và sắp xếp
            viewModel.TotalPages = (int)Math.Ceiling(totalProducts / (double)_productsPerPage);
            viewModel.CurrentPage = Math.Max(1, Math.Min(viewModel.CurrentPage, viewModel.TotalPages > 0 ? viewModel.TotalPages : 1));

            viewModel.Products = await productsQuery
                .Skip((viewModel.CurrentPage - 1) * _productsPerPage)
                .Take(_productsPerPage)
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
                    // OriginalPrice không còn được sử dụng
                }).ToListAsync();

            // Ghi lịch sử tìm kiếm với số lượng kết quả thực tế của trang hiện tại (hoặc tổng số)
            if (!string.IsNullOrWhiteSpace(searchString) && (customerIdForSearchHistory.HasValue || !string.IsNullOrEmpty(sessionIdForSearchHistory)))
            {
                try
                {
                    _context.SearchHistories.Add(new SearchHistory
                    {
                        CustomerID = customerIdForSearchHistory,
                        SessionID = customerIdForSearchHistory.HasValue ? null : sessionIdForSearchHistory,
                        SearchKeyword = searchString,
                        SearchTimestamp = DateTime.Now,
                        NumberOfResults = totalProducts // Ghi tổng số sản phẩm khớp từ khóa
                    });
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Search history saved. Keyword: {Keyword}, Total Results: {ResultsCount}", searchString, totalProducts);
                }
                catch (Exception ex_sh)
                {
                    _logger.LogError(ex_sh, "Error saving search history for keyword: {Keyword}", searchString);
                }
            }


            viewModel.Categories = new SelectList(await _context.Categories.OrderBy(c => c.CategoryName).AsNoTracking().ToListAsync(), "CategoryID", "CategoryName", viewModel.SelectedCategoryId);
            viewModel.Suppliers = new SelectList(await _context.Suppliers.OrderBy(s => s.SupplierName).AsNoTracking().ToListAsync(), "SupplierID", "SupplierName", viewModel.SelectedSupplierId);

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CustomerProducts/Index.");
            return View(new CustomerProductIndexViewModel { Categories = new SelectList(await _context.Categories.ToListAsync(), "CategoryID", "CategoryName"), Suppliers = new SelectList(await _context.Suppliers.ToListAsync(), "SupplierID", "SupplierName") });
        }
    }

    [AllowAnonymous]
    public IActionResult Search(string keyword)
    {
        return RedirectToAction(nameof(Index), new { searchString = keyword });
    }

    [AllowAnonymous]
    public async Task<IActionResult> Details(int? id, int currentPageOther = 1)
    {
        if (id == null) { _logger.LogWarning("Details - null ID."); return NotFound(); }
        try
        {
            var productType = await _context.ProductTypes.Include(p => p.Category).Include(p => p.MeasurementUnit).Include(p => p.Supplier).AsNoTracking().FirstOrDefaultAsync(m => m.ProductTypeID == id.Value);
            if (productType == null) { _logger.LogWarning("Details - ProductType ID {Id} not found.", id); return NotFound(); }

            // Ghi ViewHistory
            int? customerIdView = null;
            if (User.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(User.Identity.Name))
            {
                var customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Username == User.Identity.Name);
                if (customer != null) customerIdView = customer.CustomerID;
            }
            string? sessionIdView = HttpContext.Session.GetString("UserSessionId");
            if ((customerIdView.HasValue || !string.IsNullOrEmpty(sessionIdView)))
            {
                _context.ViewHistories.Add(new ViewHistory { CustomerID = customerIdView, SessionID = customerIdView.HasValue ? null : sessionIdView, ProductTypeID = productType.ProductTypeID, ViewTimestamp = DateTime.Now });
                await _context.SaveChangesAsync(); // SaveChanges sau khi add
            }

            // Lấy Recommended Products
            int? customerIdRec = customerIdView; // Dùng lại customerId đã lấy
            string? sessionIdRec = sessionIdView;
            var excludedFromRecs = new List<int> { productType.ProductTypeID };
            var recommendedProducts = await _recommendationService.GetRecommendationsAsync(customerIdRec, sessionIdRec, excludedFromRecs, 11);

            // Lấy "Sản phẩm khác"
            var excludedFromOthers = new HashSet<int>(excludedFromRecs);
            recommendedProducts.ForEach(rp => excludedFromOthers.Add(rp.Id));
            IQueryable<ProductType> otherProductsBaseQuery = _context.ProductTypes.Where(p => p.IsActive && !excludedFromOthers.Contains(p.ProductTypeID));
            var sameCategoryQuery = otherProductsBaseQuery.Where(p => p.CategoryID == productType.CategoryID && p.ProductTypeID != productType.ProductTypeID);
            IQueryable<ProductType> finalOtherProductsQuery = await sameCategoryQuery.AnyAsync() ? sameCategoryQuery.OrderByDescending(p => p.DateAdded) : otherProductsBaseQuery.OrderByDescending(p => p.DateAdded);
            finalOtherProductsQuery = finalOtherProductsQuery.Include(p => p.MeasurementUnit);

            var totalOtherProducts = await finalOtherProductsQuery.CountAsync();
            var totalPagesOther = (int)Math.Ceiling(totalOtherProducts / (double)_otherProductsOnDetailPage);
            currentPageOther = Math.Max(1, Math.Min(currentPageOther, totalPagesOther > 0 ? totalPagesOther : 1));
            var otherProductsVM = await finalOtherProductsQuery.Skip((currentPageOther - 1) * _otherProductsOnDetailPage).Take(_otherProductsOnDetailPage)
                .Select(p => new ProductViewModel { Id = p.ProductTypeID, Name = p.ProductName, ProductDescription = p.ProductDescription, Price = p.Price, ImagePath = p.ImagePath, MeasurementUnitName = p.MeasurementUnit.UnitName ?? "N/A", StockAmount = p.StockAmount, IsActive = p.IsActive, DateAdded = p.DateAdded }).ToListAsync();

            var viewModel = new ProductDetailViewModel { Product = productType, RecommendedProducts = recommendedProducts, OtherProducts = otherProductsVM, CurrentPageOther = currentPageOther, TotalPagesOther = totalPagesOther };
            return View(viewModel);
        }
        catch (Exception ex) { _logger.LogError(ex, "Details - Error for ProductID {Id}", id); return View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier }); }
    }
}