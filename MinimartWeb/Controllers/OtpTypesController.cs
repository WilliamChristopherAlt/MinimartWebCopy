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
    public class OtpTypesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OtpTypesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: OtpTypes
        public async Task<IActionResult> Index()
        {
            return View(await _context.OtpTypes.ToListAsync());
        }

        // GET: OtpTypes/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var otpType = await _context.OtpTypes
                .FirstOrDefaultAsync(m => m.OtpTypeID == id);
            if (otpType == null)
            {
                return NotFound();
            }

            return View(otpType);
        }

        // GET: OtpTypes/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: OtpTypes/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("OtpTypeID,OtpTypeName,Description")] OtpType otpType)
        {
            if (await _context.OtpTypes.AnyAsync(o => o.OtpTypeName == otpType.OtpTypeName))
            {
                ModelState.AddModelError("OtpTypeName", "This OTP type name already exists.");
            }

            if (ModelState.IsValid)
            {
                _context.Add(otpType);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(otpType);
        }


        // GET: OtpTypes/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var otpType = await _context.OtpTypes.FindAsync(id);
            if (otpType == null)
            {
                return NotFound();
            }
            return View(otpType);
        }

        // POST: OtpTypes/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("OtpTypeID,OtpTypeName,Description")] OtpType otpType)
        {
            if (id != otpType.OtpTypeID)
            {
                return NotFound();
            }

            if (await _context.OtpTypes.AnyAsync(o => o.OtpTypeName == otpType.OtpTypeName && o.OtpTypeID != otpType.OtpTypeID))
            {
                ModelState.AddModelError("OtpTypeName", "This OTP type name is already in use.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(otpType);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!OtpTypeExists(otpType.OtpTypeID))
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

            return View(otpType);
        }

        // GET: OtpTypes/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var otpType = await _context.OtpTypes
                .FirstOrDefaultAsync(m => m.OtpTypeID == id);
            if (otpType == null)
            {
                return NotFound();
            }

            return View(otpType);
        }

        // POST: OtpTypes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var otpType = await _context.OtpTypes.FindAsync(id);
            if (otpType != null)
            {
                _context.OtpTypes.Remove(otpType);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool OtpTypeExists(int id)
        {
            return _context.OtpTypes.Any(e => e.OtpTypeID == id);
        }
    }
}
