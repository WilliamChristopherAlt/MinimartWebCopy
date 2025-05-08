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
    public class MeasurementUnitsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MeasurementUnitsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: MeasurementUnits
        public async Task<IActionResult> Index()
        {
            return View(await _context.MeasurementUnits.ToListAsync());
        }

        // GET: MeasurementUnits/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var measurementUnit = await _context.MeasurementUnits
                .FirstOrDefaultAsync(m => m.MeasurementUnitID == id);
            if (measurementUnit == null)
            {
                return NotFound();
            }

            return View(measurementUnit);
        }

        // GET: MeasurementUnits/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: MeasurementUnits/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MeasurementUnitID,UnitName,UnitDescription,IsContinuous")] MeasurementUnit measurementUnit)
        {
            // Check if the UnitName already exists
            if (await _context.MeasurementUnits.AnyAsync(m => m.UnitName == measurementUnit.UnitName))
            {
                ModelState.AddModelError("UnitName", "The unit name already exists.");
                return View(measurementUnit); // return the same view with error
            }

            if (ModelState.IsValid)
            {
                _context.Add(measurementUnit);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(measurementUnit);
        }

        // GET: MeasurementUnits/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var measurementUnit = await _context.MeasurementUnits.FindAsync(id);
            if (measurementUnit == null)
            {
                return NotFound();
            }
            return View(measurementUnit);
        }

        // POST: MeasurementUnits/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MeasurementUnitID,UnitName,UnitDescription,IsContinuous")] MeasurementUnit measurementUnit)
        {
            if (id != measurementUnit.MeasurementUnitID)
            {
                return NotFound();
            }

            // Check if the UnitName already exists (but exclude the current record's ID to allow editing the same name)
            if (await _context.MeasurementUnits
                    .AnyAsync(m => m.UnitName == measurementUnit.UnitName && m.MeasurementUnitID != id))
            {
                ModelState.AddModelError("UnitName", "The unit name already exists.");
                return View(measurementUnit); // return the same view with error
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(measurementUnit);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MeasurementUnitExists(measurementUnit.MeasurementUnitID))
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
            return View(measurementUnit);
        }

        // GET: MeasurementUnits/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var measurementUnit = await _context.MeasurementUnits
                .FirstOrDefaultAsync(m => m.MeasurementUnitID == id);
            if (measurementUnit == null)
            {
                return NotFound();
            }

            return View(measurementUnit);
        }

        // POST: MeasurementUnits/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var measurementUnit = await _context.MeasurementUnits.FindAsync(id);
            if (measurementUnit != null)
            {
                _context.MeasurementUnits.Remove(measurementUnit);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool MeasurementUnitExists(int id)
        {
            return _context.MeasurementUnits.Any(e => e.MeasurementUnitID == id);
        }
    }
}
