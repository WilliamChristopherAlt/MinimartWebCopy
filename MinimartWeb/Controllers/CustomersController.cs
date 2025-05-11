using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MinimartWeb.Data;
using MinimartWeb.Model;
using Microsoft.AspNetCore.Hosting;
using System.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;

namespace MinimartWeb.Controllers
{
    //[Authorize(Roles = "Admin")]
    public class CustomersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        // Inject IWebHostEnvironment via constructor
        public CustomersController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: Customers
        public async Task<IActionResult> Index()
        {
            return View(await _context.Customers.ToListAsync());
        }

        // GET: Customers/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(m => m.CustomerID == id);
            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        // GET: Customers/Create
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
       [Bind("CustomerID,FirstName,LastName,Email,PhoneNumber,Username")] Customer customer,
       string Password,
       IFormFile ProfileImage)
        {
            // Remove password fields to prevent overposting
            ModelState.Remove(nameof(Customer.PasswordHash));
            ModelState.Remove(nameof(Customer.Salt));
            ModelState.Remove("ProfileImage");

            // Check for existing email, phone number, and username
            if (await _context.Customers.AnyAsync(c => c.Email == customer.Email))
            {
                ModelState.AddModelError("Email", "The email address is already taken.");
            }
            if (await _context.Customers.AnyAsync(c => c.PhoneNumber == customer.PhoneNumber))
            {
                ModelState.AddModelError("PhoneNumber", "The phone number is already in use.");
            }
            if (await _context.Customers.AnyAsync(c => c.Username == customer.Username))
            {
                ModelState.AddModelError("Username", "The username is already taken.");
            }

            // Check if password is provided
            if (string.IsNullOrWhiteSpace(Password))
            {
                ModelState.AddModelError("", "Password is required.");
            }

            if (!ModelState.IsValid)
            {
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    Console.WriteLine(error.ErrorMessage);
                }
            }

            if (!ModelState.IsValid)
            {
                return View(customer); // Return view with validation errors
            }

            // Handle image upload or set default
            if (ProfileImage != null && ProfileImage.Length > 0)
            {
                var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "users");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var fileExtension = Path.GetExtension(ProfileImage.FileName);
                var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await ProfileImage.CopyToAsync(fileStream);
                }

                customer.ImagePath = uniqueFileName;
            }
            else
            {
                customer.ImagePath = "default.jpg"; // Use "default.jpg" instead of "default.png"
            }

            // Hash password and generate salt
            (byte[] passwordHash, byte[] salt) = GeneratePasswordHashAndSalt(Password);
            customer.PasswordHash = passwordHash;
            customer.Salt = salt;

            _context.Add(customer);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // GET: Customers/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return NotFound();
            }
            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id,
            [Bind("CustomerID,FirstName,LastName,Email,PhoneNumber,Username,IsEmailVerified")] Customer customer,
            string Password,
            IFormFile ProfileImage)
        {
            if (id != customer.CustomerID)
                return NotFound();

            // Remove fields that are not posted or manually handled
            ModelState.Remove(nameof(Customer.PasswordHash));
            ModelState.Remove(nameof(Customer.Salt));
            ModelState.Remove(nameof(Customer.ImagePath));
            ModelState.Remove(nameof(Customer.EmailVerifiedAt));

            if (string.IsNullOrWhiteSpace(Password))
                ModelState.Remove("Password");

            // Check for uniqueness (excluding current user)
            if (await _context.Customers.AnyAsync(c => c.Email == customer.Email && c.CustomerID != customer.CustomerID))
                ModelState.AddModelError("Email", "The email address is already taken.");

            if (await _context.Customers.AnyAsync(c => c.PhoneNumber == customer.PhoneNumber && c.CustomerID != customer.CustomerID))
                ModelState.AddModelError("PhoneNumber", "The phone number is already in use.");

            if (await _context.Customers.AnyAsync(c => c.Username == customer.Username && c.CustomerID != customer.CustomerID))
                ModelState.AddModelError("Username", "The username is already taken.");

            if (!ModelState.IsValid)
                return View(customer);

            var customerFromDb = await _context.Customers.FindAsync(id);
            if (customerFromDb == null)
                return NotFound();

            // Update editable fields
            customerFromDb.FirstName = customer.FirstName;
            customerFromDb.LastName = customer.LastName;
            customerFromDb.Email = customer.Email;
            customerFromDb.PhoneNumber = customer.PhoneNumber;
            customerFromDb.Username = customer.Username;

            // ✅ Update IsEmailVerified + Timestamp
            customerFromDb.IsEmailVerified = customer.IsEmailVerified;
            if (customer.IsEmailVerified && customerFromDb.EmailVerifiedAt == null)
            {
                customerFromDb.EmailVerifiedAt = DateTime.UtcNow;
            }
            else if (!customer.IsEmailVerified)
            {
                customerFromDb.EmailVerifiedAt = null;
            }

            // ✅ Handle image upload
            if (ProfileImage != null && ProfileImage.Length > 0)
            {
                var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "users");
                Directory.CreateDirectory(uploadsFolder); // Create folder if missing

                var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(ProfileImage.FileName);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await ProfileImage.CopyToAsync(fileStream);
                }

                customerFromDb.ImagePath = uniqueFileName;
            }

            // ✅ Update password if provided
            if (!string.IsNullOrWhiteSpace(Password))
            {
                (byte[] passwordHash, byte[] salt) = GeneratePasswordHashAndSalt(Password);
                customerFromDb.PasswordHash = passwordHash;
                customerFromDb.Salt = salt;
            }

            try
            {
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CustomerExists(customer.CustomerID))
                    return NotFound();
                throw;
            }
        }


        // GET: Customers/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(m => m.CustomerID == id);
            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        // POST: Customers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer != null)
            {
                _context.Customers.Remove(customer);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool CustomerExists(int id)
        {
            return _context.Customers.Any(e => e.CustomerID == id);
        }

        // Utility method to create a password hash and salt
        private (byte[] passwordHash, byte[] salt) GeneratePasswordHashAndSalt(string password)
        {
            var salt = new byte[16];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt); // Fill the salt with random bytes
            }

            using var sha256 = SHA256.Create();
            var combinedBytes = Encoding.UTF8.GetBytes(password).Concat(salt).ToArray();
            var computedHash = sha256.ComputeHash(combinedBytes);

            return (computedHash, salt);
        }
    }
}
