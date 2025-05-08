using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging; // <-- THÊM USING NÀY
using MinimartWeb.Data;
using System.Threading.Tasks;
using System.Linq; // Thêm using Linq nếu cần cho Search
using MinimartWeb.Model; // Đảm bảo namespace Model đúng
using MinimartWeb.Models;

namespace MinimartWeb.Controllers
{
    public class CustomerProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CustomerProductsController> _logger; // <-- KHAI BÁO LOGGER

        // Sửa Constructor để nhận cả DbContext và ILogger
        public CustomerProductsController(ApplicationDbContext context, ILogger<CustomerProductsController> logger)
        {
            _context = context;
            _logger = logger; // <-- GÁN LOGGER
        }

        // GET: CustomerProducts (Giả sử bạn muốn trang này công khai)
        [AllowAnonymous] // <-- Thêm AllowAnonymous nếu Index là công khai
        public async Task<IActionResult> Index()
        {
            try // Thêm try-catch để xử lý lỗi tốt hơn
            {
                var products = await _context.ProductTypes
                    .Where(p => p.IsActive && p.StockAmount > 0)
                    .Include(p => p.Category)
                    .Include(p => p.Supplier)
                    .Include(p => p.MeasurementUnit)
                    .AsNoTracking() // Tối ưu đọc
                    .ToListAsync();

                return View(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving products for CustomerProducts Index.");
                // Có thể trả về một trang lỗi thân thiện hơn
                return View("Error", new ErrorViewModel { /* Thêm thông tin lỗi nếu cần */ });
            }
        }

        // Action Search (Đã có AllowAnonymous từ trước)
        [AllowAnonymous]
        public async Task<IActionResult> Search(string keyword)
        {
            try
            {
                ViewData["Keyword"] = keyword;
                var query = _context.ProductTypes
                                .Include(p => p.Category) // Include lại nếu cần hiển thị trong kết quả
                                .Include(p => p.MeasurementUnit)
                                .Include(p => p.Supplier)
                                .Where(p => p.IsActive) // Chỉ tìm sản phẩm active?
                                .AsNoTracking();

                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    query = query.Where(p => p.ProductName.Contains(keyword) ||
                                            (p.ProductDescription != null && p.ProductDescription.Contains(keyword)));
                }
                else // Nếu keyword rỗng, có thể quay về trang Index hoặc hiển thị tất cả
                {
                    return RedirectToAction(nameof(Index)); // Quay về Index nếu không có keyword
                    // Hoặc: query = query; // Hiển thị tất cả nếu muốn
                }


                var results = await query.ToListAsync();

                // Quan trọng: Đảm bảo View SearchResults tồn tại hoặc sửa lại để dùng View Index
                return View("SearchResults", results); // Giả sử có View SearchResults.cshtml
                                                       // Hoặc return View("Index", results); // Nếu View Index có thể hiển thị kết quả tìm kiếm
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during product search for keyword: {Keyword}", keyword);
                return View("Error", new ErrorViewModel());
            }
        }


        // Action Details (Đã có AllowAnonymous từ trước)
        [AllowAnonymous]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                _logger.LogWarning("Details action called with null ID."); // Giờ đây _logger đã tồn tại
                return NotFound();
            }

            try
            {
                var productType = await _context.ProductTypes
                    .Include(p => p.Category)
                    .Include(p => p.MeasurementUnit)
                    .Include(p => p.Supplier)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.ProductTypeID == id);

                if (productType == null)
                {
                    _logger.LogWarning("ProductType with ID {Id} not found.", id); // Giờ đây _logger đã tồn tại
                    return NotFound();
                }

                return View(productType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving details for ProductType ID: {Id}", id);
                return View("Error", new ErrorViewModel());
            }
        }

        // Các actions khác nếu có...
        // Ví dụ CartController sẽ cần inject DbContext và có thể cả Logger
    }
}