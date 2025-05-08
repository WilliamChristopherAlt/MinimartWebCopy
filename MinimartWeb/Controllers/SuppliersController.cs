using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MinimartWeb.Data;
using MinimartWeb.Model;

namespace MinimartWeb.Controllers
{
    [Authorize(Roles = "Admin")]
    public class SuppliersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SuppliersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Suppliers
        public async Task<IActionResult> Index()
        {
            return View(await _context.Suppliers.ToListAsync());
        }

        // GET: Suppliers/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(m => m.SupplierID == id);
            if (supplier == null)
            {
                return NotFound();
            }

            return View(supplier);
        }

        // GET: Suppliers/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Suppliers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("SupplierID,SupplierName,SupplierPhoneNumber,SupplierAddress,SupplierEmail")] Supplier supplier)
        {
            // Check if a supplier with the same name already exists
            if (await _context.Suppliers.AnyAsync(s => s.SupplierName == supplier.SupplierName))
            {
                ModelState.AddModelError("SupplierName", "Supplier with this name already exists.");
            }

            // Check if a supplier with the same phone number already exists
            if (await _context.Suppliers.AnyAsync(s => s.SupplierPhoneNumber == supplier.SupplierPhoneNumber))
            {
                ModelState.AddModelError("SupplierPhoneNumber", "Supplier with this phone number already exists.");
            }

            // Check if a supplier with the same email already exists
            if (await _context.Suppliers.AnyAsync(s => s.SupplierEmail == supplier.SupplierEmail))
            {
                ModelState.AddModelError("SupplierEmail", "Supplier with this email already exists.");
            }

            // If model state is valid, add the supplier
            if (ModelState.IsValid)
            {
                _context.Add(supplier);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            // If not valid, return to the Create view with validation errors
            return View(supplier);
        }


        // GET: Suppliers/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null)
            {
                return NotFound();
            }
            return View(supplier);
        }

        // POST: Suppliers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("SupplierID,SupplierName,SupplierPhoneNumber,SupplierAddress,SupplierEmail")] Supplier supplier)
        {
            if (id != supplier.SupplierID)
            {
                return NotFound();
            }

            // Check if any other supplier (excluding the current one) has the same name
            if (await _context.Suppliers.AnyAsync(s => s.SupplierName == supplier.SupplierName && s.SupplierID != id))
            {
                ModelState.AddModelError("SupplierName", "Supplier with this name already exists.");
            }

            // Check if any other supplier (excluding the current one) has the same phone number
            if (await _context.Suppliers.AnyAsync(s => s.SupplierPhoneNumber == supplier.SupplierPhoneNumber && s.SupplierID != id))
            {
                ModelState.AddModelError("SupplierPhoneNumber", "Supplier with this phone number already exists.");
            }

            // Check if any other supplier (excluding the current one) has the same email
            if (await _context.Suppliers.AnyAsync(s => s.SupplierEmail == supplier.SupplierEmail && s.SupplierID != id))
            {
                ModelState.AddModelError("SupplierEmail", "Supplier with this email already exists.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(supplier);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SupplierExists(supplier.SupplierID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }

            // If not valid, return to the Edit view with validation errors
            return View(supplier);
        }

        // GET: Suppliers/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(m => m.SupplierID == id);
            if (supplier == null)
            {
                return NotFound();
            }

            return View(supplier);
        }

        // POST: Suppliers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier != null)
            {
                _context.Suppliers.Remove(supplier);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool SupplierExists(int id)
        {
            return _context.Suppliers.Any(e => e.SupplierID == id);
        }
    }
}
