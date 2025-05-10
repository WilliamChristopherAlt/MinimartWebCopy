// Trong Controllers/CustomerProductsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MinimartWeb.Data;
using MinimartWeb.Model;
using MinimartWeb.Models;
using MinimartWeb.ViewModels; // Thêm using này
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

public class CustomerProductsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CustomerProductsController> _logger;
    private readonly int _otherProductsPerPage = 15; // Số "Sản phẩm khác" trên mỗi trang

    public CustomerProductsController(ApplicationDbContext context, ILogger<CustomerProductsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index() { /* ... giữ nguyên ... */  return View(await _context.ProductTypes.Where(p => p.IsActive).Include(p => p.MeasurementUnit).ToListAsync()); }

    [AllowAnonymous]
    public async Task<IActionResult> Search(string keyword, int currentPage = 1) // Thêm currentPage cho phân trang "Sản phẩm khác"
    {
        _logger.LogInformation("Search action called with keyword: '{Keyword}', CurrentPage: {CurrentPage}", keyword, currentPage);
        var viewModel = new SearchResultsViewModel
        {
            Keyword = keyword ?? string.Empty, // Đảm bảo keyword không null
            CurrentPage = currentPage
        };

        try
        {
            // 1. LẤY KẾT QUẢ TÌM KIẾM
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var searchResultsQuery = _context.ProductTypes
                    .Where(p => p.IsActive &&
                                (p.ProductName.Contains(keyword) ||
                                 (p.ProductDescription != null && p.ProductDescription.Contains(keyword))))
                    .Include(p => p.MeasurementUnit) // Include các thông tin cần thiết cho _RegularProductCard
                    .Include(p => p.Category)       // (Ví dụ)
                    .AsNoTracking();

                // Chuyển đổi kết quả tìm kiếm sang ProductViewModel
                viewModel.SearchResults = await searchResultsQuery
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
                        // OriginalPrice = p.OriginalPrice,
                        // Không cần tính TotalUnitsSold, UnitsSoldThisMonth ở đây nếu card không hiển thị
                    })
                    .OrderByDescending(p => p.DateAdded) // Sắp xếp kết quả tìm kiếm nếu muốn
                    .ToListAsync();

                _logger.LogInformation("Found {Count} products for keyword '{Keyword}'.", viewModel.SearchResults.Count, keyword);

                // Ghi lịch sử tìm kiếm (như cũ)
                int? customerId = null;
                if (User.Identity != null && User.Identity.IsAuthenticated && !string.IsNullOrEmpty(User.Identity.Name))
                {
                    var customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Username == User.Identity.Name);
                    if (customer != null) customerId = customer.CustomerID;
                }
                string? sessionId = HttpContext.Session.GetString("UserSessionId");
                if (customerId.HasValue || !string.IsNullOrEmpty(sessionId))
                {
                    _context.SearchHistories.Add(new SearchHistory
                    {
                        CustomerID = customerId,
                        SessionID = customerId.HasValue ? null : sessionId,
                        SearchKeyword = keyword,
                        SearchTimestamp = DateTime.Now,
                        NumberOfResults = viewModel.SearchResults.Count
                    });
                    await _context.SaveChangesAsync();
                }
            }
            else
            {
                _logger.LogInformation("Search keyword is empty, no search results to fetch.");
                // Không cần làm gì thêm cho SearchResults nếu keyword rỗng
            }


            // 2. LẤY DANH SÁCH "SẢN PHẨM KHÁC" (có phân trang)
            // Lấy ID của các sản phẩm đã có trong kết quả tìm kiếm để có thể loại trừ (tùy chọn)
            var searchedProductIds = viewModel.SearchResults.Select(sr => sr.Id).ToHashSet();

            var otherProductsQuery = _context.ProductTypes
                                    .Where(p => p.IsActive && !searchedProductIds.Contains(p.ProductTypeID)) // Tùy chọn: Loại trừ SP đã có trong kết quả search
                                    .Include(p => p.MeasurementUnit)
                                    .OrderByDescending(p => p.DateAdded) // Ví dụ: Sắp xếp theo mới nhất
                                    .AsNoTracking();

            var totalOtherProducts = await otherProductsQuery.CountAsync();
            viewModel.TotalPages = (int)Math.Ceiling(totalOtherProducts / (double)_otherProductsPerPage);
            if (viewModel.CurrentPage < 1) viewModel.CurrentPage = 1;
            if (viewModel.CurrentPage > viewModel.TotalPages && viewModel.TotalPages > 0) viewModel.CurrentPage = viewModel.TotalPages;


            viewModel.OtherProducts = await otherProductsQuery
                                        .Skip((viewModel.CurrentPage - 1) * _otherProductsPerPage)
                                        .Take(_otherProductsPerPage)
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
            _logger.LogInformation("Fetched {Count} other products for page {Page}.", viewModel.OtherProducts.Count, viewModel.CurrentPage);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during product search or fetching other products for keyword: {Keyword}", keyword);
            // Vẫn cố gắng trả về viewModel với những gì đã có để View không bị lỗi hoàn toàn
            // Hoặc return View("Error", new ErrorViewModel ...);
        }

        return View("SearchResults", viewModel); // Trả về View SearchResults.cshtml
    }

    [AllowAnonymous]
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) { _logger.LogWarning("Details called with null ID."); return NotFound(); }

        try
        {
            var productType = await _context.ProductTypes
                .Include(p => p.Category).Include(p => p.MeasurementUnit).Include(p => p.Supplier)
                .AsNoTracking().FirstOrDefaultAsync(m => m.ProductTypeID == id);

            if (productType == null) { _logger.LogWarning("ProductType ID {Id} not found.", id); return NotFound(); }

            // --- GHI LỊCH SỬ XEM ---
            try
            {
                int? customerId = null;
                if (User.Identity != null && User.Identity.IsAuthenticated && !string.IsNullOrEmpty(User.Identity.Name))
                {
                    var customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Username == User.Identity.Name);
                    if (customer != null) customerId = customer.CustomerID;
                }
                string? sessionId = HttpContext.Session.GetString("UserSessionId");

                if ((customerId.HasValue || !string.IsNullOrEmpty(sessionId)) && productType != null)
                {
                    var viewHistoryEntry = new ViewHistory
                    {
                        CustomerID = customerId,
                        SessionID = customerId.HasValue ? null : sessionId,
                        ProductTypeID = productType.ProductTypeID, // Quan trọng: phải là ID của sản phẩm đang xem
                        ViewTimestamp = DateTime.Now
                    };
                    _context.ViewHistories.Add(viewHistoryEntry);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("View history saved for ProductID: {ProductId}, User/Session: {UserSession}", productType.ProductTypeID, customerId ?? (object)sessionId ?? "Unknown");
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "Error saving view history for ProductID {ProductId}", id); }

            return View(productType);
        }
        catch (Exception ex) { _logger.LogError(ex, "Error retrieving details for ProductType ID: {Id}", id); return View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier }); }
    }
}