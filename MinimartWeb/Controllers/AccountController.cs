// Trong file: Controllers/AccountController.cs

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization; // Cần cho [AllowAnonymous] và [Authorize]
using Microsoft.AspNetCore.Mvc;
using MinimartWeb.Data;
using MinimartWeb.Models;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Linq;
using Microsoft.Extensions.Logging; // Thêm using cho ILogger
using Microsoft.AspNetCore.Http; // Cần cho StatusCodes
using Microsoft.AspNetCore.Mvc.ModelBinding; // Cần cho ModelStateDictionary

public class AccountController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AccountController> _logger;

    public AccountController(ApplicationDbContext context, ILogger<AccountController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // --- [HttpGet] Login: Chuyển hướng về Home nếu chưa đăng nhập ---
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectBasedOnRole(User);
        }
        // Chuyển hướng về trang chủ thay vì hiển thị View Login.cshtml
        return RedirectToAction("Index", "Home");
    }

    // --- [HttpPost] Login: Trả về JSON cho AJAX ---
    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelStateToDictionary(ModelState));
        }

        ClaimsIdentity identity = null;
        string role = "";
        string redirectUrl = Url.Action("Index", "Home") ?? "/";

        try
        {
            if (model.UserType == "Customer")
            {
                var customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Username == model.Username);
                // Sử dụng tên property ID đúng của bạn (ví dụ: customer.Id)
                if (customer == null || !VerifyPassword(model.Password, customer.PasswordHash, customer.Salt))
                {
                    return BadRequest(new { message = "Tên đăng nhập hoặc mật khẩu không đúng." });
                }
                role = "Customer";
                // Sử dụng tên property ID đúng của bạn (ví dụ: customer.Id)
                identity = CreateClaimsIdentity(customer.Username, role, customer.CustomerID.ToString());
                redirectUrl = Url.Action("Index", "CustomerProducts") ?? "/CustomerProducts";
            }
            else if (model.UserType == "Employee")
            {
                var employeeAccount = await _context.EmployeeAccounts.AsNoTracking().Include(ea => ea.Employee).ThenInclude(e => e.Role).FirstOrDefaultAsync(ea => ea.Username == model.Username);
                // Sử dụng tên property ID đúng của bạn (ví dụ: employeeAccount.Employee.EmployeeId)
                if (employeeAccount?.Employee?.Role == null || !VerifyPassword(model.Password, employeeAccount.PasswordHash, employeeAccount.Salt))
                {
                    return BadRequest(new { message = "Tên đăng nhập hoặc mật khẩu không đúng." });
                }
                role = employeeAccount.Employee.Role.RoleName;
                // Sử dụng tên property ID đúng của bạn (ví dụ: employeeAccount.Employee.EmployeeId)
                identity = CreateClaimsIdentity(employeeAccount.Username, role, employeeAccount.Employee.EmployeeID.ToString());
                if (role == "Admin" || role == "Staff") { redirectUrl = Url.Action("Index", "ProductTypes") ?? "/ProductTypes"; }
            }
            else
            {
                return BadRequest(new { message = "Loại người dùng không hợp lệ." });
            }

            var principal = new ClaimsPrincipal(identity);
            var authProperties = new AuthenticationProperties { /* IsPersistent = model.RememberMe */ };
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

            _logger.LogInformation("User {Username} logged in successfully.", model.Username);
            return Ok(new { success = true, redirectUrl = redirectUrl });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login error for user {Username}.", model.Username);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi hệ thống khi đăng nhập." });
        }
    }

    // --- [HttpPost] Logout: Trả về Ok() cho AJAX ---
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        _logger.LogInformation("User logged out.");
        return Ok();
    }

    // --- AccessDenied ---
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    // --- VerifyPassword ---
    private bool VerifyPassword(string password, byte[] storedHash, byte[] storedSalt) { if (storedHash == null || storedSalt == null || storedHash.Length == 0 || storedSalt.Length == 0) { return false; } using var sha256 = SHA256.Create(); var combinedBytes = Encoding.UTF8.GetBytes(password).Concat(storedSalt).ToArray(); var computedHash = sha256.ComputeHash(combinedBytes); return storedHash.SequenceEqual(computedHash); }

    // --- CreateClaimsIdentity ---
    private ClaimsIdentity CreateClaimsIdentity(string username, string role, string userId) { return new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, username), new Claim(ClaimTypes.Role, role), new Claim("UserId", userId) }, CookieAuthenticationDefaults.AuthenticationScheme); }

    // --- RedirectBasedOnRole ---
    private IActionResult RedirectBasedOnRole(ClaimsPrincipal user) { if (user.IsInRole("Admin") || user.IsInRole("Staff")) { return RedirectToAction("Index", "ProductTypes"); } else if (user.IsInRole("Customer")) { return RedirectToAction("Index", "CustomerProducts"); } return RedirectToAction("Index", "Home"); }

    // --- ModelStateToDictionary ---
    private Dictionary<string, string[]> ModelStateToDictionary(ModelStateDictionary modelState) { return modelState.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()).Where(m => m.Value.Length > 0).ToDictionary(kvp => kvp.Key, kvp => kvp.Value); }
}