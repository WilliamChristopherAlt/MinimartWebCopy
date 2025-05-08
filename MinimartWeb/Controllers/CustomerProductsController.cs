using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MinimartWeb.Data;
using System.Threading.Tasks;

namespace MinimartWeb.Controllers
{
    public class CustomerProductsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CustomerProductsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: CustomerProducts
        public async Task<IActionResult> Index()
        {
            var products = await _context.ProductTypes
                .Where(p => p.IsActive && p.StockAmount > 0)
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .Include(p => p.MeasurementUnit)
                .ToListAsync();

            return View(products);
        }

        public async Task<IActionResult> Search(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return RedirectToAction("Index", "CustomerProducts");
            }

            var results = await _context.ProductTypes
                .Include(p => p.MeasurementUnit)
                .Where(p => p.ProductName.Contains(keyword) || p.ProductDescription.Contains(keyword))
                .ToListAsync();

            ViewBag.SearchKeyword = keyword;
            return View("SearchResults", results);
        }
    }
}
