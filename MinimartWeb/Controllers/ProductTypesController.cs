﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MinimartWeb.Data;
using MinimartWeb.Model;

namespace MinimartWeb.Controllers
{
    [Authorize(Roles = "Admin, Staff")]
    public class ProductTypesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductTypesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: ProductTypes
        [AllowAnonymous]
        public async Task<IActionResult> Index(int page = 1)
        {
            int pageSize = 15;

            var query = _context.ProductTypes
                .Include(p => p.Category)
                .Include(p => p.MeasurementUnit)
                .Include(p => p.Supplier);

            int totalItems = await query.CountAsync();
            var products = await query
                .OrderBy(p => p.ProductTypeID) // or any other sort
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            return View(products);
        }


        // GET: ProductTypes/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var productType = await _context.ProductTypes
                .Include(p => p.Category)
                .Include(p => p.MeasurementUnit)
                .Include(p => p.Supplier)
                .Include(p => p.ProductTags)
                    .ThenInclude(pt => pt.Tag) // include tag names
                .FirstOrDefaultAsync(m => m.ProductTypeID == id);

            if (productType == null)
            {
                return NotFound();
            }

            return View(productType);
        }


        [HttpGet]
        public IActionResult Create()
        {
            ViewData["CategoryID"] = new SelectList(_context.Categories, "CategoryID", "CategoryName");
            ViewData["MeasurementUnitID"] = new SelectList(_context.MeasurementUnits, "MeasurementUnitID", "UnitName");
            ViewData["SupplierID"] = new SelectList(_context.Suppliers, "SupplierID", "SupplierName");

            // Preload tag names into ViewBag
            ViewBag.AllTags = _context.Tags.Select(t => t.TagName).ToList();

            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ProductTypeID,ProductName,ProductDescription,CategoryID,SupplierID,Price,StockAmount,MeasurementUnitID,ExpirationDurationDays,IsActive,ImagePath")] ProductType productType, IFormFile ImageUpload, string tagsInput)
        {
            if (await _context.ProductTypes.AnyAsync(p => p.ProductName == productType.ProductName))
            {
                ModelState.AddModelError("ProductName", "Product Name already exists.");
            }

            ModelState.Remove("ImagePath");
            ModelState.Remove("Category");
            ModelState.Remove("MeasurementUnit");
            ModelState.Remove("Supplier");
            ModelState.Remove("DateAdded");
            ModelState.Remove("tagsInput");

            if (ImageUpload != null && ImageUpload.Length > 0)
            {
                var fileExtension = Path.GetExtension(ImageUpload.FileName);
                var newFileName = $"{Guid.NewGuid()}{fileExtension}";
                var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "products", newFileName);

                using (var fileStream = new FileStream(uploadPath, FileMode.Create))
                {
                    await ImageUpload.CopyToAsync(fileStream);
                }

                productType.ImagePath = newFileName;
            }

            if (string.IsNullOrEmpty(productType.ImagePath))
            {
                ModelState.AddModelError("ImagePath", "Product image is required.");
            }

            if (!string.IsNullOrWhiteSpace(tagsInput))
            {
                // Only allow letters, spaces, and commas
                var invalidTagPattern = new Regex(@"[^\p{L},\s]", RegexOptions.Compiled);
                if (invalidTagPattern.IsMatch(tagsInput))
                {
                    ViewData["TagError"] = "Tags can only contain letters, commas, and spaces.";
                    ModelState.AddModelError("tagsInput", "Tags can only contain letters, commas, and spaces.");
                }
            }

            if (ModelState.IsValid)
            {
                productType.DateAdded = DateTime.Today;

                _context.ProductTypes.Add(productType);
                await _context.SaveChangesAsync();

                // Process tags
                if (!string.IsNullOrWhiteSpace(tagsInput))
                {
                    var tags = tagsInput
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => t.Trim().ToLower())
                        .Distinct();

                    var tagEntities = new List<Tag>();

                    foreach (var tagName in tags)
                    {
                        var existingTag = await _context.Tags.FirstOrDefaultAsync(t => t.TagName.ToLower() == tagName);
                        if (existingTag == null)
                        {
                            var newTag = new Tag { TagName = tagName };
                            tagEntities.Add(newTag);
                            _context.Tags.Add(newTag);
                        }
                        else
                        {
                            tagEntities.Add(existingTag);
                        }
                    }

                    // Save all new tags at once
                    await _context.SaveChangesAsync();

                    // Now create the ProductTags using resolved TagIDs
                    foreach (var tag in tagEntities)
                    {
                        _context.ProductTags.Add(new ProductTag
                        {
                            ProductTypeID = productType.ProductTypeID,
                            TagID = tag.TagID
                        });
                    }

                    await _context.SaveChangesAsync();

                }

                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    Console.WriteLine(error.ErrorMessage);
                }
            }

            // Reload dropdowns
            ViewData["CategoryID"] = new SelectList(_context.Categories, "CategoryID", "CategoryName", productType.CategoryID);
            ViewData["MeasurementUnitID"] = new SelectList(_context.MeasurementUnits, "MeasurementUnitID", "UnitName", productType.MeasurementUnitID);
            ViewData["SupplierID"] = new SelectList(_context.Suppliers, "SupplierID", "SupplierName", productType.SupplierID);

            return View(productType);
        }


        // GET: ProductTypes/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var productType = await _context.ProductTypes
                .Include(p => p.ProductTags)
                .ThenInclude(pt => pt.Tag)
                .FirstOrDefaultAsync(p => p.ProductTypeID == id);

            if (productType == null) return NotFound();

            ViewData["CategoryID"] = new SelectList(_context.Categories, "CategoryID", "CategoryName", productType.CategoryID);
            ViewData["MeasurementUnitID"] = new SelectList(_context.MeasurementUnits, "MeasurementUnitID", "UnitName", productType.MeasurementUnitID);
            ViewData["SupplierID"] = new SelectList(_context.Suppliers, "SupplierID", "SupplierName", productType.SupplierID);

            // Suggestion list for autocomplete
            ViewBag.AllTags = _context.Tags.Select(t => t.TagName).ToList();

            // Existing tags of the product (as comma-separated string)
            ViewBag.TagString = string.Join(", ", productType.ProductTags.Select(pt => pt.Tag.TagName));

            return View(productType);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ProductTypeID,ProductName,ProductDescription,CategoryID,SupplierID,Price,StockAmount,MeasurementUnitID,ExpirationDurationDays,IsActive,ImagePath")] ProductType productType, IFormFile ImageUpload, string tagsInput)
        {
            if (id != productType.ProductTypeID)
            {
                return NotFound();
            }

            ModelState.Remove("Category");
            ModelState.Remove("Supplier");
            ModelState.Remove("MeasurementUnit");
            ModelState.Remove("ImageUpload");
            ModelState.Remove("tagsInput");
            ModelState.Remove("DateAdded");

            if (await _context.ProductTypes.AnyAsync(p => p.ProductName == productType.ProductName && p.ProductTypeID != productType.ProductTypeID))
            {
                ModelState.AddModelError("ProductName", "Product Name already exists.");
            }

            if (ImageUpload != null && ImageUpload.Length > 0)
            {
                var fileExtension = Path.GetExtension(ImageUpload.FileName);
                var newFileName = $"{Guid.NewGuid()}{fileExtension}";
                var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "products", newFileName);

                using (var fileStream = new FileStream(uploadPath, FileMode.Create))
                {
                    await ImageUpload.CopyToAsync(fileStream);
                }

                productType.ImagePath = newFileName;
            }

            if (string.IsNullOrEmpty(productType.ImagePath))
            {
                ModelState.AddModelError("ImagePath", "Product image is required.");
            }

            if (!string.IsNullOrWhiteSpace(tagsInput))
            {
                var invalidTagPattern = new Regex(@"[^\p{L},\s]", RegexOptions.Compiled);
                if (invalidTagPattern.IsMatch(tagsInput))
                {
                    ViewData["TagError"] = "Tags can only contain letters, commas, and spaces.";
                    ModelState.AddModelError("tagsInput", "Tags can only contain letters, commas, and spaces.");
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(productType);
                    await _context.SaveChangesAsync();

                    // --- Update Tags ---
                    // Remove old tags
                    var existingTags = _context.ProductTags.Where(pt => pt.ProductTypeID == productType.ProductTypeID);
                    _context.ProductTags.RemoveRange(existingTags);
                    await _context.SaveChangesAsync();

                    if (!string.IsNullOrWhiteSpace(tagsInput))
                    {
                        var tags = tagsInput
                            .Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(t => t.Trim().ToLower())
                            .Distinct();

                        foreach (var tagName in tags)
                        {
                            var existingTag = await _context.Tags.FirstOrDefaultAsync(t => t.TagName.ToLower() == tagName);
                            if (existingTag == null)
                            {
                                existingTag = new Tag { TagName = tagName };
                                _context.Tags.Add(existingTag);
                                await _context.SaveChangesAsync();
                            }

                            _context.ProductTags.Add(new ProductTag
                            {
                                ProductTypeID = productType.ProductTypeID,
                                TagID = existingTag.TagID
                            });
                        }

                        await _context.SaveChangesAsync();
                    }

                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductTypeExists(productType.ProductTypeID))
                        return NotFound();
                    else
                        throw;
                }
            }

            // Reload dropdowns
            ViewData["CategoryID"] = new SelectList(_context.Categories, "CategoryID", "CategoryName", productType.CategoryID);
            ViewData["MeasurementUnitID"] = new SelectList(_context.MeasurementUnits, "MeasurementUnitID", "UnitName", productType.MeasurementUnitID);
            ViewData["SupplierID"] = new SelectList(_context.Suppliers, "SupplierID", "SupplierName", productType.SupplierID);

            return View(productType);
        }


        // GET: ProductTypes/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var productType = await _context.ProductTypes
                .Include(p => p.Category)
                .Include(p => p.MeasurementUnit)
                .Include(p => p.Supplier)
                .FirstOrDefaultAsync(m => m.ProductTypeID == id);
            if (productType == null)
            {
                return NotFound();
            }

            return View(productType);
        }

        // POST: ProductTypes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var productType = await _context.ProductTypes.FindAsync(id);
            if (productType != null)
            {
                _context.ProductTypes.Remove(productType);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ProductTypeExists(int id)
        {
            return _context.ProductTypes.Any(e => e.ProductTypeID == id);
        }
    }
}
