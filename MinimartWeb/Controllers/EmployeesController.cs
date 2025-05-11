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
    //[Authorize(Roles = "Admin")]
    public class EmployeesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EmployeesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Employees
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Employees.Include(e => e.Role);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: Employees/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var employee = await _context.Employees
                .Include(e => e.Role)
                .FirstOrDefaultAsync(m => m.EmployeeID == id);
            if (employee == null)
            {
                return NotFound();
            }

            return View(employee);
        }

        // GET: Employees/Create
        [HttpGet]
        public IActionResult Create()
        {
            // Populate roles for the dropdown list
            var roles = _context.EmployeeRoles
                .Select(role => new SelectListItem
                {
                    Value = role.RoleID.ToString(),
                    Text = role.RoleName
                })
                .ToList();

            // Pass the list of roles to the view
            ViewData["Roles"] = roles;

            return View();
        }


        // POST: Employees/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("EmployeeID,FirstName,LastName,Email,PhoneNumber,Gender,BirthDate,CitizenID,Salary,HireDate,RoleID,ProfileImage")] Employee employee, IFormFile ProfileImage)
        {
            ModelState.Remove(nameof(Employee.Role));
            ModelState.Remove("ProfileImage");

            // Check for uniqueness of Email, PhoneNumber, and CitizenID
            if (await _context.Employees.AnyAsync(e => e.Email == employee.Email))
            {
                ModelState.AddModelError("Email", "The email address is already taken.");
            }

            if (await _context.Employees.AnyAsync(e => e.PhoneNumber == employee.PhoneNumber))
            {
                ModelState.AddModelError("PhoneNumber", "The phone number is already taken.");
            }

            if (await _context.Employees.AnyAsync(e => e.CitizenID == employee.CitizenID))
            {
                ModelState.AddModelError("CitizenID", "The Citizen ID is already taken.");
            }

            if (ModelState.IsValid)
            {
                // Handle image upload if a file is provided
                if (ProfileImage != null && ProfileImage.Length > 0)
                {
                    var fileName = Path.GetFileNameWithoutExtension(ProfileImage.FileName);
                    var fileExtension = Path.GetExtension(ProfileImage.FileName);
                    var newFileName = $"{Guid.NewGuid()}{fileExtension}";

                    if (string.IsNullOrEmpty(employee.ImagePath))
                    {
                        employee.ImagePath = $"{Guid.NewGuid()}{fileExtension}";
                    }

                    var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "users", newFileName);

                    using (var fileStream = new FileStream(uploadPath, FileMode.Create))
                    {
                        await ProfileImage.CopyToAsync(fileStream);
                    }

                    employee.ImagePath = $"/{newFileName}";
                }

                _context.Add(employee);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            // If model state is not valid, log errors for debugging
            if (!ModelState.IsValid)
            {
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    Console.WriteLine(error.ErrorMessage);
                }
            }

            // Repopulate roles for the dropdown list
            var roles = _context.EmployeeRoles
                .Select(role => new SelectListItem
                {
                    Value = role.RoleID.ToString(),
                    Text = role.RoleName
                })
                .ToList();

            ViewData["Roles"] = roles;

            return View(employee);
        }


        // GET: Employees/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var employee = await _context.Employees.FindAsync(id);
            if (employee == null)
            {
                return NotFound();
            }

            // Set default image if no image path is found
            if (string.IsNullOrEmpty(employee.ImagePath))
            {
                employee.ImagePath = "default.jpg";
            }

            // Populate the Role dropdown for the Edit form
            var roles = _context.EmployeeRoles
                .Select(role => new SelectListItem
                {
                    Value = role.RoleID.ToString(),
                    Text = role.RoleName
                })
                .ToList();

            // Pass the list of roles to the view
            ViewData["Roles"] = roles;

            return View(employee);
        }

        // POST: Employees/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("EmployeeID,FirstName,LastName,Email,PhoneNumber,Gender,BirthDate,CitizenID,Salary,HireDate,RoleID,ImagePath")] Employee employee, IFormFile ProfileImage)
        {
            if (id != employee.EmployeeID)
            {
                return NotFound();
            }

            ModelState.Remove(nameof(Employee.Role));
            ModelState.Remove("ProfileImage");

            // Check for uniqueness of Email, PhoneNumber, and CitizenID, excluding the current employee's ID for edit
            if (await _context.Employees
                .AnyAsync(e => e.Email == employee.Email && e.EmployeeID != id))
            {
                ModelState.AddModelError("Email", "The email address is already taken.");
            }

            if (await _context.Employees
                .AnyAsync(e => e.PhoneNumber == employee.PhoneNumber && e.EmployeeID != id))
            {
                ModelState.AddModelError("PhoneNumber", "The phone number is already taken.");
            }

            if (await _context.Employees
                .AnyAsync(e => e.CitizenID == employee.CitizenID && e.EmployeeID != id))
            {
                ModelState.AddModelError("CitizenID", "The Citizen ID is already taken.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Handle image upload if a new file is provided
                    if (ProfileImage != null && ProfileImage.Length > 0)
                    {
                        var fileName = Path.GetFileNameWithoutExtension(ProfileImage.FileName);
                        var fileExtension = Path.GetExtension(ProfileImage.FileName);
                        var newFileName = $"{Guid.NewGuid()}{fileExtension}";

                        var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "users", newFileName);

                        using (var fileStream = new FileStream(uploadPath, FileMode.Create))
                        {
                            await ProfileImage.CopyToAsync(fileStream);
                        }

                        employee.ImagePath = $"{newFileName}";
                    }
                    else
                    {
                        // No new image uploaded, keep the existing one
                        var existingEmployee = await _context.Employees
                            .AsNoTracking()
                            .FirstOrDefaultAsync(e => e.EmployeeID == id);

                        if (existingEmployee != null)
                        {
                            employee.ImagePath = existingEmployee.ImagePath;
                        }
                    }

                    _context.Update(employee);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EmployeeExists(employee.EmployeeID))
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

            // If model state is not valid, log errors for debugging
            if (!ModelState.IsValid)
            {
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    Console.WriteLine(error.ErrorMessage);
                }
            }

            // Repopulate roles dropdown in case of errors
            var roles = _context.EmployeeRoles
                .Select(role => new SelectListItem
                {
                    Value = role.RoleID.ToString(),
                    Text = role.RoleName
                })
                .ToList();

            ViewData["Roles"] = roles;

            return View(employee);
        }

        // GET: Employees/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var employee = await _context.Employees
                .Include(e => e.Role)
                .FirstOrDefaultAsync(m => m.EmployeeID == id);
            if (employee == null)
            {
                return NotFound();
            }

            return View(employee);
        }

        // POST: Employees/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee != null)
            {
                _context.Employees.Remove(employee);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool EmployeeExists(int id)
        {
            return _context.Employees.Any(e => e.EmployeeID == id);
        }
    }
}
