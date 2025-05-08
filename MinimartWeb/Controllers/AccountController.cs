using Microsoft.AspNetCore.Authentication.Cookies; // AIzaSyBBIBGwM6GN-r8kdCnA79QBF7I-Albvuds
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MinimartWeb.Models;
using MinimartWeb.Data;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using System.Security.Cryptography;

public class AccountController : Controller
{
    private readonly ApplicationDbContext _context;

    public AccountController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (User.IsInRole("Admin") || User.IsInRole("Staff"))
            {
                return RedirectToAction("Index", "ProductTypes");
            }
            else if (User.IsInRole("Customer"))
            {
                return RedirectToAction("Index", "CustomerProducts");
            }
        }

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            Console.WriteLine("ModelState Errors: " + string.Join(", ", errors));
            return View(model);
        }

        ClaimsIdentity identity = null;
        string role = "";

        if (model.UserType == "Customer")
        {
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Username == model.Username);

            if (customer == null || !VerifyPassword(model.Password, customer.PasswordHash, customer.Salt))
            {
                ModelState.AddModelError("", "Invalid username or password.");
                return View(model);
            }

            role = "Customer";
            identity = new ClaimsIdentity(new[]
            {
            new Claim(ClaimTypes.Name, model.Username),
            new Claim(ClaimTypes.Role, role)
        }, CookieAuthenticationDefaults.AuthenticationScheme);
        }
        else if (model.UserType == "Employee")
        {
            var employeeAccount = await _context.EmployeeAccounts
                .Include(ea => ea.Employee)
                .ThenInclude(e => e.Role)
                .FirstOrDefaultAsync(ea => ea.Username == model.Username);

            if (employeeAccount == null || !VerifyPassword(model.Password, employeeAccount.PasswordHash, employeeAccount.Salt))
            {
                ModelState.AddModelError("", "Invalid username or password.");
                return View(model);
            }

            // ✅ Update last login timestamp
            employeeAccount.LastLogin = DateTime.Now;
            await _context.SaveChangesAsync();

            role = employeeAccount.Employee.Role.RoleName;
            identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, model.Username),
                new Claim(ClaimTypes.Role, role)
            }, CookieAuthenticationDefaults.AuthenticationScheme);

            foreach (var claim in identity.Claims)
            {
                Console.WriteLine($"Claim Type: {claim.Type}, Value: {claim.Value}");
            }
        }

        else
        {
            ModelState.AddModelError("", "Invalid user type.");
            return View(model);
        }

        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        // Role-based redirection after login
        if (role == "Admin" || role == "Staff")
        {
            return RedirectToAction("Index", "ProductTypes");
        }
        else if (role == "Customer")
        {
            return RedirectToAction("Index", "CustomerProducts");
        }

        // Default fallback
        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpGet]
    public IActionResult LogoutConfirmation()
    {
        return View(); // Show a confirmation page
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        return RedirectToAction("Login");
    }


    public IActionResult AccessDenied() => View();

    private bool VerifyPassword(string password, byte[] storedHash, byte[] storedSalt)
    {
        using var sha256 = SHA256.Create();
        var combinedBytes = Encoding.UTF8.GetBytes(password).Concat(storedSalt).ToArray();
        var computedHash = sha256.ComputeHash(combinedBytes);
        bool isValid = storedHash.SequenceEqual(computedHash);
        Console.WriteLine($"Password verification: {(isValid ? "Success" : "Failed")}");
        return isValid;
    }
}
