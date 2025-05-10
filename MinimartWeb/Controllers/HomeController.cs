using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MinimartWeb.Data;
using MinimartWeb.Model;
using MinimartWeb.Models;
using MinimartWeb.Services;
using MinimartWeb.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

[AllowAnonymous]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _context;
    private readonly IRecommendationService _recommendationService;
    private readonly int _productsPerPage = 25;

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext context, IRecommendationService recommendationService)
    {
        _logger = logger;
        _context = context;
        _recommendationService = recommendationService;
    }

    public async Task<IActionResult> Index(int currentPage = 1)
    {
        _logger.LogInformation("Home/Index page requested. CurrentPage: {CurrentPage}", currentPage);
        try
        {
            string? sessionId = HttpContext.Session.GetString("UserSessionId");
            if (string.IsNullOrEmpty(sessionId))
            {
                sessionId = Guid.NewGuid().ToString();
                HttpContext.Session.SetString("UserSessionId", sessionId);
                _logger.LogInformation("New SessionId created: {SessionId}", sessionId);
            }

            var homePageViewModel = new HomePageViewModel();

            var productViewModelsQuery = _context.ProductTypes
                .Where(p => p.IsActive)
                .Include(p => p.MeasurementUnit)
                // .Include(p => p.Category) // Chỉ include nếu service cần
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
                    TotalUnitsSold = p.SaleDetails.Sum(sd => (int?)sd.Quantity) ?? 0, // Tính TotalUnitsSold
                    UnitsSoldThisMonth = p.SaleDetails.Where(sd => sd.Sale.SaleDate.Month == DateTime.Now.Month && sd.Sale.SaleDate.Year == DateTime.Now.Year).Sum(sd => (int?)sd.Quantity) ?? 0 // Tính UnitsSoldThisMonth
                })
                .AsNoTracking();

            var productViewModels = await productViewModelsQuery.ToListAsync();
            _logger.LogInformation("Fetched {Count} total active productViewModels.", productViewModels.Count);

            int numberOfHotDeals = 11;
            homePageViewModel.HotDeals = productViewModels
                                            .OrderByDescending(p => p.UnitsSoldThisMonth)
                                            .ThenByDescending(p => p.TotalUnitsSold)
                                            .Take(numberOfHotDeals)
                                            .ToList();
            var hotDealIds = homePageViewModel.HotDeals.Select(h => h.Id).ToList();
            _logger.LogInformation("Generated {Count} Hot Deals. IDs: {HotDealIds}", homePageViewModel.HotDeals.Count, string.Join(",", hotDealIds));

            int? customerId = null;
            if (User.Identity != null && User.Identity.IsAuthenticated && !string.IsNullOrEmpty(User.Identity.Name))
            {
                var customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Username == User.Identity.Name);
                if (customer != null) customerId = customer.CustomerID;
            }

            homePageViewModel.RecommendedProducts = await _recommendationService.GetRecommendationsAsync(customerId, sessionId, hotDealIds, 11);
            _logger.LogInformation("Generated {Count} Recommended Products for CustomerID: {CustomerId}, SessionID: {SessionId}.", homePageViewModel.RecommendedProducts.Count, customerId, sessionId);

            var regularProductsQuery = productViewModels
                                        .OrderByDescending(p => p.DateAdded);
            var totalRegularProducts = regularProductsQuery.Count(); // Count trước khi phân trang
            homePageViewModel.TotalPages = (int)Math.Ceiling(totalRegularProducts / (double)_productsPerPage);
            homePageViewModel.CurrentPage = currentPage;
            homePageViewModel.RegularProducts = regularProductsQuery
                                                .Skip((currentPage - 1) * _productsPerPage)
                                                .Take(_productsPerPage)
                                                .ToList();
            _logger.LogInformation("Generated {Count} Regular Products for page {CurrentPage}.", homePageViewModel.RegularProducts.Count, currentPage);

            return View(homePageViewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading Home page data.");
            return View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }

    [AllowAnonymous] public IActionResult Privacy() => View();
    [AllowAnonymous] public IActionResult About() => View();
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)] public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}