// Trong file: Controllers/AccountController.cs

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http; // Cho StatusCodes và Session (nếu dùng Session ở đây)
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding; // Cho ModelStateDictionary
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging; // Cho ILogger
using MinimartWeb.Data;
using MinimartWeb.Models; // Cho LoginViewModel, ErrorViewModel
using MinimartWeb.Model;  // Cho Customer, EmployeeAccount, Employee, EmployeeRole
using System;
using System.Collections.Generic; // Cho Dictionary
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;


public class AccountController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AccountController> _logger;

    public AccountController(ApplicationDbContext context, ILogger<AccountController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // --- [HttpGet] Login: Xử lý khi người dùng truy cập trực tiếp /Account/Login ---
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login()
    {
        // Nếu đã đăng nhập, chuyển hướng dựa trên vai trò (tránh họ ở lại trang Login)
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectBasedOnRole(User);
        }
        // Nếu chưa đăng nhập và cố tình vào /Account/Login, chuyển về trang chủ.
        // Người dùng sẽ phải dùng panel AJAX trên header để đăng nhập.
        return RedirectToAction("Index", "Home");
    }

    // --- [HttpPost] Login: Xử lý đăng nhập từ panel AJAX, luôn trả về JSON ---
    [AllowAnonymous] // Cho phép truy cập khi chưa đăng nhập
    [HttpPost]
    [ValidateAntiForgeryToken] // Quan trọng để chống CSRF
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            // Trả về lỗi validation dưới dạng JSON để JavaScript hiển thị
            return BadRequest(ModelStateToDictionary(ModelState));
        }

        ClaimsIdentity? identity = null; // Khởi tạo là nullable
        string role = string.Empty; // Khởi tạo chuỗi rỗng
        string redirectUrl = Url.Action("Index", "Home") ?? "/"; // LUÔN CHUYỂN VỀ TRANG CHỦ

        try
        {
            if (model.UserType == "Customer")
            {
                var customer = await _context.Customers.AsNoTracking()
                                       .FirstOrDefaultAsync(c => c.Username == model.Username);

                if (customer == null || !VerifyPassword(model.Password, customer.PasswordHash, customer.Salt))
                {
                    _logger.LogWarning("Login attempt failed for customer username: {Username}", model.Username);
                    return BadRequest(new { message = "Tên đăng nhập hoặc mật khẩu không đúng." });
                }
                role = "Customer"; // Role này sẽ được lưu vào cookie
                identity = CreateClaimsIdentity(customer.Username, role, customer.CustomerID.ToString()); // Sử dụng CustomerID
            }
            else if (model.UserType == "Employee")
            {
                var employeeAccount = await _context.EmployeeAccounts.AsNoTracking()
                                            .Include(ea => ea.Employee)
                                                .ThenInclude(e => e.Role) // Cần Role để lấy RoleName
                                            .FirstOrDefaultAsync(ea => ea.Username == model.Username);

                if (employeeAccount?.Employee?.Role == null || !VerifyPassword(model.Password, employeeAccount.PasswordHash, employeeAccount.Salt))
                {
                    _logger.LogWarning("Login attempt failed for employee username: {Username}", model.Username);
                    return BadRequest(new { message = "Tên đăng nhập hoặc mật khẩu không đúng." });
                }
                role = employeeAccount.Employee.Role.RoleName; // Lấy RoleName từ EmployeeRole
                identity = CreateClaimsIdentity(employeeAccount.Username, role, employeeAccount.Employee.EmployeeID.ToString()); // Sử dụng EmployeeID
            }
            else
            {
                _logger.LogWarning("Invalid UserType specified during login: {UserType}", model.UserType);
                return BadRequest(new { message = "Loại người dùng không hợp lệ." });
            }

            // Đăng nhập người dùng
            var principal = new ClaimsPrincipal(identity);
            var authProperties = new AuthenticationProperties
            {
                // IsPersistent = model.RememberMe, // Bật nếu có checkbox "Remember Me"
                // ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7) // Ví dụ thời gian cookie
            };
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

            _logger.LogInformation("User {Username} (Role: {Role}) logged in successfully. Redirecting to {RedirectUrl}", model.Username, role, redirectUrl);
            return Ok(new { success = true, redirectUrl = redirectUrl }); // Luôn trả về URL trang chủ
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred during login for user {Username}.", model.Username);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Đã có lỗi hệ thống xảy ra trong quá trình đăng nhập. Vui lòng thử lại sau." });
        }
    }

    // --- [HttpPost] Logout: Xử lý đăng xuất từ panel AJAX, trả về Ok() ---
    [Authorize] // Yêu cầu người dùng phải đăng nhập để thực hiện
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        string? userName = User.Identity?.Name; // Lấy tên user trước khi logout để log
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        _logger.LogInformation("User {UserName} logged out successfully.", userName ?? "Unknown");
        return Ok(new { message = "Đăng xuất thành công." }); // Trả về JSON thành công
    }


    // --- AccessDenied: Trang hiển thị khi truy cập bị từ chối ---
    [AllowAnonymous] // Cho phép truy cập trang này ngay cả khi chưa đăng nhập
    public IActionResult AccessDenied()
    {
        _logger.LogWarning("Access denied for user: {UserName}", User.Identity?.Name ?? "Anonymous");
        return View(); // Trả về View AccessDenied.cshtml
    }

    // --- Hàm xác thực mật khẩu ---
    private bool VerifyPassword(string password, byte[] storedHash, byte[] storedSalt)
    {
        if (storedHash == null || storedSalt == null || storedHash.Length == 0 || storedSalt.Length == 0)
        {
            _logger.LogWarning("Password verification attempted with null or empty stored hash/salt.");
            return false;
        }
        using var sha256 = SHA256.Create();
        var combinedBytes = Encoding.UTF8.GetBytes(password).Concat(storedSalt).ToArray();
        var computedHash = sha256.ComputeHash(combinedBytes);
        return storedHash.SequenceEqual(computedHash);
    }

    // --- Hàm tạo ClaimsIdentity ---
    private ClaimsIdentity CreateClaimsIdentity(string username, string role, string userId)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, username), // Tên đăng nhập, dùng để User.Identity.Name
            new Claim(ClaimTypes.Role, role),     // Vai trò của người dùng
            new Claim(ClaimTypes.NameIdentifier, userId) // ID duy nhất của người dùng (Customer ID hoặc Employee ID)
            // Bạn có thể thêm các claim khác nếu cần, ví dụ: Email, FullName...
            // new Claim("UserId", userId) // Hoặc một claim tùy chỉnh tên là "UserId"
        };
        return new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    }

    // --- Hàm chuyển hướng dựa trên vai trò (dùng cho [HttpGet] Login và các trường hợp khác nếu cần) ---
    private IActionResult RedirectBasedOnRole(ClaimsPrincipal user)
    {
        if (user.IsInRole("Admin") || user.IsInRole("Staff"))
        {
            return RedirectToAction("Index", "ProductTypes"); // Hoặc trang quản trị mặc định
        }
        else if (user.IsInRole("Customer"))
        {
            return RedirectToAction("Index", "CustomerProducts"); // Hoặc trang sản phẩm của khách hàng
        }
        return RedirectToAction("Index", "Home"); // Trang chủ mặc định
    }

    // --- Hàm chuyển ModelState lỗi thành Dictionary để trả về JSON ---
    private Dictionary<string, string[]> ModelStateToDictionary(ModelStateDictionary modelState)
    {
        return modelState
            .Where(x => x.Value.Errors.Count > 0)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
            );
    }
}