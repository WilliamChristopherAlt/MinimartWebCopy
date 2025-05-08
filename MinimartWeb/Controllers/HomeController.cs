// Trong file: Controllers/HomeController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MinimartWeb.Data;
using MinimartWeb.Models; // Hoặc MinimartWeb.Model
using MinimartWeb.ViewModels; // Thêm using cho ViewModels
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;         // Thêm using Linq
using System;             // Thêm using cho DateTime

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _context;

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        // --- Lấy dữ liệu sản phẩm cơ bản ---
        var allActiveProducts = await _context.ProductTypes
                                        .Where(p => p.IsActive) // Chỉ lấy sản phẩm đang hoạt động
                                        .Select(p => new // Chọn các cột cần thiết ban đầu
                                        {
                                            p.ProductTypeID,
                                            p.ProductName,
                                            p.Price,
                                            p.ImagePath,
                                            // Giả sử bạn có cột OriginalPrice trong ProductTypes
                                            // Nếu không, bạn cần logic khác để xác định giá gốc
                                            // OriginalPrice = p.OriginalPrice // Ví dụ
                                            // Thêm các trường cần thiết khác nếu có (ví dụ: cờ FlashSale)
                                            IsFlashSale = false // Mặc định hoặc lấy từ DB
                                        })
                                        .AsNoTracking()
                                        .ToListAsync();

        // --- Tính toán số lượng đã bán ---
        var productIds = allActiveProducts.Select(p => p.ProductTypeID).ToList();

        // Tính tổng số lượng bán từ trước đến nay
        var totalSalesData = await _context.SaleDetails
                                    .Where(sd => productIds.Contains(sd.ProductTypeID))
                                    .GroupBy(sd => sd.ProductTypeID)
                                    .Select(g => new {
                                        ProductTypeId = g.Key,
                                        TotalUnitsSold = g.Sum(x => (int)x.Quantity) // Ép kiểu nếu Quantity là decimal
                                    })
                                    .ToListAsync();

        // Tính số lượng bán trong tháng hiện tại
        var now = DateTime.Now;
        var startOfMonth = new DateTime(now.Year, now.Month, 1);
        var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1); // Ngày cuối cùng của tháng

        var monthlySalesData = await _context.SaleDetails
                                    .Include(sd => sd.Sale) // Include bảng Sales để lọc theo SaleDate
                                    .Where(sd => productIds.Contains(sd.ProductTypeID) &&
                                                  sd.Sale.SaleDate >= startOfMonth && sd.Sale.SaleDate <= endOfMonth)
                                    .GroupBy(sd => sd.ProductTypeID)
                                    .Select(g => new {
                                        ProductTypeId = g.Key,
                                        UnitsSoldThisMonth = g.Sum(x => (int)x.Quantity) // Ép kiểu nếu Quantity là decimal
                                    })
                                    .ToListAsync();

        // --- Tạo danh sách ProductViewModel hoàn chỉnh ---
        var productViewModels = allActiveProducts.Select(p => {
            var totalSale = totalSalesData.FirstOrDefault(s => s.ProductTypeId == p.ProductTypeID);
            var monthlySale = monthlySalesData.FirstOrDefault(s => s.ProductTypeId == p.ProductTypeID);

            return new ProductViewModel
            {
                Id = p.ProductTypeID,
                Name = p.ProductName,
                Price = p.Price,
                ImagePath = p.ImagePath,
                // OriginalPrice = p.OriginalPrice, // Gán giá gốc nếu có
                TotalUnitsSold = totalSale?.TotalUnitsSold ?? 0,
                UnitsSoldThisMonth = monthlySale?.UnitsSoldThisMonth ?? 0,
                IsFlashSale = p.IsFlashSale // Gán cờ FlashSale nếu có
            };
        }).ToList();


        // --- Tạo HomePageViewModel ---
        var homePageViewModel = new HomePageViewModel();

        // Lấy top N sản phẩm bán chạy nhất tháng làm Hot Deals
        int numberOfHotDeals = 5; // Số lượng sản phẩm Hot Deal muốn hiển thị
        homePageViewModel.HotDeals = productViewModels
                                        .OrderByDescending(p => p.UnitsSoldThisMonth)
                                        .Take(numberOfHotDeals)
                                        .ToList();

        // Lấy tất cả sản phẩm làm Regular Products (bao gồm cả hot deals, hoặc loại trừ nếu muốn)
        // Để đơn giản, ta hiển thị tất cả
        homePageViewModel.RegularProducts = productViewModels
                                               .OrderByDescending(p => p.TotalUnitsSold) // Sắp xếp theo tổng số bán
                                               .ToList();
        // Hoặc nếu muốn loại trừ Hot Deals khỏi Regular Products:
        // var hotDealIds = homePageViewModel.HotDeals.Select(h => h.Id).ToHashSet();
        // homePageViewModel.RegularProducts = productViewModels
        //                                     .Where(p => !hotDealIds.Contains(p.Id))
        //                                     .OrderByDescending(p => p.TotalUnitsSold)
        //                                     .ToList();


        // Truyền ViewModel vào View
        return View(homePageViewModel);
    }

    [AllowAnonymous] public IActionResult Privacy() => View();
    [AllowAnonymous] public IActionResult About() => View();
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)] public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}