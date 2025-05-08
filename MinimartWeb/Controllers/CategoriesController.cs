using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization; // Thêm using
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MinimartWeb.Data;
using MinimartWeb.Model; // Đảm bảo namespace đúng

namespace MinimartWeb.Controllers
{
    // Bỏ [Authorize(Roles = "Admin")] ở đây

    public class CategoriesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CategoriesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Categories
        [AllowAnonymous] // <-- Cho phép xem danh sách
        public async Task<IActionResult> Index()
        {
            // Bỏ kiểm tra quyền thủ công ở đây
            // if (!User.IsInRole("Admin"))
            // {
            //     return RedirectToAction("AccessDenied", "Account");
            // }
            return View(await _context.Categories.ToListAsync());
        }

        // GET: Categories/Details/5
        [AllowAnonymous] // Giữ AllowAnonymous nếu muốn xem chi tiết công khai
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var category = await _context.Categories.FirstOrDefaultAsync(m => m.CategoryID == id);
            if (category == null) return NotFound();
            return View(category);
        }

        // GET: Categories/Create
        [Authorize(Roles = "Admin")] // <-- Thêm Authorize cho Admin
        public IActionResult Create()
        {
            return View();
        }

        // POST: Categories/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")] // <-- Thêm Authorize cho Admin
        public async Task<IActionResult> Create([Bind("CategoryName,CategoryDescription")] Category category)
        {
            // Logic Create giữ nguyên
            if (!ModelState.IsValid) return View(category);
            // Kiểm tra trùng tên trước khi thêm
            if (await _context.Categories.AnyAsync(c => c.CategoryName == category.CategoryName))
            {
                ModelState.AddModelError("CategoryName", "Category name already exists.");
                return View(category);
            }
            try { _context.Add(category); await _context.SaveChangesAsync(); return RedirectToAction(nameof(Index)); } catch (DbUpdateException ex) { /* Xử lý lỗi DB */ Console.WriteLine($"DB Error: {ex.InnerException?.Message ?? ex.Message}"); ModelState.AddModelError("", "Database error occurred."); } catch (Exception ex) { Console.WriteLine($"Error: {ex.Message}"); ModelState.AddModelError("", "An unexpected error occurred."); }
            return View(category);
        }


        // GET: Categories/Edit/5
        [Authorize(Roles = "Admin")] // <-- Thêm Authorize cho Admin
        public async Task<IActionResult> Edit(int? id)
        {
            // Logic Edit GET giữ nguyên
            if (id == null) return NotFound();
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();
            return View(category);
        }

        // POST: Categories/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")] // <-- Thêm Authorize cho Admin
        public async Task<IActionResult> Edit(int id, [Bind("CategoryID,CategoryName,CategoryDescription")] Category category)
        {
            // Logic Edit POST giữ nguyên (có thể thêm kiểm tra trùng tên khi sửa)
            if (id != category.CategoryID) return NotFound();
            if (!ModelState.IsValid) return View(category);
            // Kiểm tra trùng tên (trừ chính nó)
            if (await _context.Categories.AnyAsync(c => c.CategoryName == category.CategoryName && c.CategoryID != id))
            {
                ModelState.AddModelError("CategoryName", "Category name already exists.");
                return View(category);
            }
            try { _context.Update(category); await _context.SaveChangesAsync(); return RedirectToAction(nameof(Index)); } catch (DbUpdateConcurrencyException) { if (!CategoryExists(category.CategoryID)) return NotFound(); else throw; } catch (DbUpdateException ex) { Console.WriteLine($"DB Error: {ex.InnerException?.Message ?? ex.Message}"); ModelState.AddModelError("", "Database error occurred."); } catch (Exception ex) { Console.WriteLine($"Error: {ex.Message}"); ModelState.AddModelError("", "An unexpected error occurred."); }
            return View(category);
        }


        // GET: Categories/Delete/5
        [Authorize(Roles = "Admin")] // <-- Thêm Authorize cho Admin
        public async Task<IActionResult> Delete(int? id)
        {
            // Logic Delete GET giữ nguyên
            if (id == null) return NotFound();
            var category = await _context.Categories.FirstOrDefaultAsync(m => m.CategoryID == id);
            if (category == null) return NotFound();
            return View(category);
        }

        // POST: Categories/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")] // <-- Thêm Authorize cho Admin
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            // Logic Delete POST giữ nguyên
            var category = await _context.Categories.FindAsync(id);
            if (category != null) { _context.Categories.Remove(category); }
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool CategoryExists(int id)
        {
            return _context.Categories.Any(e => e.CategoryID == id);
        }
    }
}