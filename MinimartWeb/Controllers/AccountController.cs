// Trong file: Controllers/AccountController.cs

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MinimartWeb.Data;
using MinimartWeb.Models;     // Chứa LoginViewModel, RegisterViewModel, ErrorViewModel
using MinimartWeb.ViewModels; // Chứa VerifyOtpViewModel, VerifyLoginOtpViewModel
using MinimartWeb.Model;      // Chứa Customer, EmployeeAccount, OtpRequest, OtpType
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MinimartWeb.Services; // Cho IEmailSender

public class AccountController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AccountController> _logger;
    private readonly IEmailSender _emailSender;
    private readonly IWebHostEnvironment _webHostEnvironment;

    private const int MaxOtpResendAttempts = 5;
    private const int OtpResendLockoutMinutes = 15;

    public AccountController(ApplicationDbContext context,
                           ILogger<AccountController> logger,
                           IEmailSender emailSender,
                           IWebHostEnvironment webHostEnvironment)
    {
        _context = context;
        _logger = logger;
        _emailSender = emailSender;
        _webHostEnvironment = webHostEnvironment;
    }

    // --- [HttpGet] Login: Truy cập trực tiếp /Account/Login ---
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectBasedOnRole(User);
        return RedirectToAction("Index", "Home");
    }

    //// --- [HttpPost] Login: Bước 1 - Xác thực Username/Password & Gửi OTP 2FA ---
    //[AllowAnonymous]
    //[HttpPost]
    //[ValidateAntiForgeryToken]
    //public async Task<IActionResult> Login(LoginViewModel model)
    //{
    //    if (!ModelState.IsValid)
    //    {
    //        return BadRequest(new { errors = ModelStateToDictionary(ModelState) });
    //    }

    //    _logger.LogInformation("Login POST: Bắt đầu xử lý đăng nhập cho UserType: {UserType}, Username: {Username}", model.UserType, model.Username);

    //    string? userEmailForOtp = null;
    //    int? customerIdForOtpRecord = null;
    //    int? employeeAccountIdForOtpRecord = null;
    //    string usernameForClaims = model.Username;
    //    string roleForClaims = string.Empty;
    //    string userIdForClaims = string.Empty;

    //    try
    //    {
    //        if (model.UserType == "Customer")
    //        {
    //            var customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Username == model.Username);
    //            if (customer == null || !VerifyPassword(model.Password, customer.PasswordHash, customer.Salt))
    //            {
    //                _logger.LogWarning("Đăng nhập thất bại (Bước 1 - Khách hàng): Sai thông tin cho {Username}", model.Username);
    //                return BadRequest(new { message = "Tên đăng nhập hoặc mật khẩu không đúng." });
    //            }
    //            if (!customer.IsEmailVerified)
    //            {
    //                _logger.LogWarning("Đăng nhập (Bước 1 - Khách hàng): Email chưa xác minh cho {Username}", model.Username);
    //                TempData["UnverifiedEmail"] = customer.Email;
    //                return BadRequest(new { message = "Tài khoản của bạn chưa được xác minh email ban đầu. Vui lòng kiểm tra email hoặc yêu cầu gửi lại mã xác minh.", needsInitialVerification = true, email = customer.Email });
    //            }
    //            userEmailForOtp = customer.Email;
    //            customerIdForOtpRecord = customer.CustomerID;
    //            roleForClaims = "Customer";
    //            userIdForClaims = customer.CustomerID.ToString();
    //        }
    //        else if (model.UserType == "Employee") // <<<==== PHẦN XỬ LÝ CHO NHÂN VIÊN (BƯỚC 1)
    //        {
    //            _logger.LogInformation("Login (Employee): Attempting to authenticate employee {Username}", model.Username);
    //            var employeeAccount = await _context.EmployeeAccounts.AsNoTracking()
    //                                        .Include(ea => ea.Employee) // Nạp thông tin Employee
    //                                            .ThenInclude(e => e.Role) // Từ Employee, nạp thông tin Role
    //                                        .FirstOrDefaultAsync(ea => ea.Username == model.Username);

    //            if (employeeAccount == null)
    //            {
    //                _logger.LogWarning("Login (Employee): Tài khoản nhân viên không tồn tại: {Username}", model.Username);
    //                return BadRequest(new { success = false, message = "Tên đăng nhập hoặc mật khẩu không đúng." });
    //            }
    //            if (employeeAccount.Employee == null) // Kiểm tra quan trọng
    //            {
    //                _logger.LogError("Login (Employee): Dữ liệu Employee liên kết với EmployeeAccountID {AccountId} là null cho Username {Username}.", employeeAccount.AccountID, model.Username);
    //                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "Lỗi dữ liệu hệ thống (Employee null). Vui lòng liên hệ quản trị viên." });
    //            }
    //            if (employeeAccount.Employee.Role == null) // Kiểm tra quan trọng
    //            {
    //                _logger.LogError("Login (Employee): Dữ liệu Role liên kết với EmployeeID {EmployeeId} là null cho Username {Username}.", employeeAccount.EmployeeID, model.Username);
    //                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "Lỗi dữ liệu hệ thống (Role null). Vui lòng liên hệ quản trị viên." });
    //            }

    //            if (!VerifyPassword(model.Password, employeeAccount.PasswordHash, employeeAccount.Salt))
    //            {
    //                _logger.LogWarning("Login (Employee): Sai mật khẩu cho {Username}", model.Username);
    //                return BadRequest(new { success = false, message = "Tên đăng nhập hoặc mật khẩu không đúng." });
    //            }

    //            if (!employeeAccount.IsActive)
    //            {
    //                _logger.LogWarning("Login (Employee): Tài khoản nhân viên {Username} không hoạt động.", model.Username);
    //                return BadRequest(new { success = false, message = "Tài khoản nhân viên này đã bị khóa hoặc chưa được kích hoạt." });
    //            }

    //            // Giả sử EmployeeAccount cũng có IsEmailVerified và EmailVerifiedAt nếu cần
    //            // var empAccEntityType = _context.Model.FindEntityType(typeof(EmployeeAccount));
    //            // var isEmpEmailVerifiedProp = empAccEntityType?.FindProperty("IsEmailVerified");
    //            // if (isEmpEmailVerifiedProp != null && !employeeAccount.IsEmailVerified)
    //            // {
    //            //    _logger.LogWarning("Login (Employee): Email nhân viên chưa xác minh cho {Username}", model.Username);
    //            //    return BadRequest(new { success = false, message = "Email của tài khoản nhân viên này chưa được xác minh.", 
    //            //                            needsInitialVerification = true, 
    //            //                            email = employeeAccount.Employee.Email });
    //            // }


    //            userEmailForOtp = employeeAccount.Employee.Email;       // Email để gửi OTP
    //            employeeAccountIdForOtpRecord = employeeAccount.AccountID; // Dùng để tạo OtpRequest
    //            roleForClaims = employeeAccount.Employee.Role.RoleName;    // Vai trò (Admin, Staff)
    //            userIdForClaims = employeeAccount.EmployeeID.ToString();   // EmployeeID để lưu vào Claims
    //            usernameForClaims = employeeAccount.Username; // Lấy username chính xác từ DB
    //        }
    //        else
    //        {
    //            return BadRequest(new { success = false, message = "Loại người dùng không hợp lệ." });
    //        }

    //        // -- Tiếp tục logic gửi OTP chung cho cả Customer và Employee --
    //        if (string.IsNullOrEmpty(userEmailForOtp))
    //        {
    //            _logger.LogError("Không thể xác định email để gửi OTP cho Username: {Username}, UserType: {UserType}", model.Username, model.UserType);
    //            return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "Lỗi hệ thống: Không thể gửi mã xác thực." });
    //        }

    //        // Tạo và gửi OTP
    //        string otpCode = GenerateOtp();
    //        var otpType = await _context.OtpTypes.FirstOrDefaultAsync(ot => ot.OtpTypeName == "LoginTwoFactorVerification");
    //        if (otpType == null)
    //        {
    //            _logger.LogCritical("CRITICAL: OtpType 'LoginTwoFactorVerification' not found.");
    //            return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "Lỗi cấu hình hệ thống (OTP Type)." });
    //        }

    //        // Vô hiệu hóa OTP 2FA cũ chưa dùng của user này
    //        var existingUnusedOtps = _context.OtpRequests
    //            .Where(o => o.OtpTypeID == otpType.OtpTypeID && !o.IsUsed && o.ExpirationTime > DateTime.UtcNow);
    //        if (customerIdForOtpRecord.HasValue) existingUnusedOtps = existingUnusedOtps.Where(o => o.CustomerID == customerIdForOtpRecord.Value);
    //        else if (employeeAccountIdForOtpRecord.HasValue) existingUnusedOtps = existingUnusedOtps.Where(o => o.EmployeeAccountID == employeeAccountIdForOtpRecord.Value);

    //        var oldOtpsToInvalidate = await existingUnusedOtps.ToListAsync();
    //        foreach (var oldOtp in oldOtpsToInvalidate)
    //        {
    //            oldOtp.IsUsed = true;
    //            oldOtp.Status = "InvalidatedByNewLogin";
    //        }

    //        var otpRequest = new OtpRequest
    //        {
    //            CustomerID = customerIdForOtpRecord,
    //            EmployeeAccountID = employeeAccountIdForOtpRecord,
    //            OtpTypeID = otpType.OtpTypeID,
    //            OtpCode = otpCode,
    //            RequestTime = DateTime.UtcNow,
    //            ExpirationTime = DateTime.UtcNow.AddMinutes(5),
    //            IsUsed = false,
    //            Status = "PendingLogin2FA"
    //        };
    //        _context.OtpRequests.Add(otpRequest);
    //        await _context.SaveChangesAsync();

    //        string emailSubject = "Mã Xác Thực Đăng Nhập MiniMart";
    //        string emailMessage = $"<p>Xin chào {usernameForClaims},</p><p>Mã xác thực đăng nhập (2FA) của bạn là: <strong>{otpCode}</strong>. Mã này sẽ hết hạn sau 5 phút.</p>";
    //        await _emailSender.SendEmailAsync(userEmailForOtp, emailSubject, emailMessage);

    //        _logger.LogInformation("Bước 1 Đăng nhập ({UserType}): OTP 2FA đã gửi đến {Email} cho {UsernameForClaims}", model.UserType, userEmailForOtp, usernameForClaims);

    //        TempData["2FA_Attempt_Username"] = usernameForClaims; // Dùng username từ DB
    //        TempData["2FA_Attempt_UserType"] = model.UserType;
    //        TempData["2FA_Attempt_EmailForDisplay"] = userEmailForOtp;
    //        TempData["2FA_Attempt_RememberMe"] = model.RememberMe;
    //        TempData["2FA_Attempt_Role"] = roleForClaims;
    //        TempData["2FA_Attempt_UserId"] = userIdForClaims; // CustomerID hoặc EmployeeID

    //        return Ok(new { success = true, redirectUrl = Url.Action(nameof(VerifyLoginOtp)) });
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "Lỗi không mong muốn trong Bước 1 Đăng nhập cho Username: {Username}.", model.Username);
    //        return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "Lỗi hệ thống không mong muốn. Vui lòng thử lại." });
    //    }
    //}

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { success = false, errors = ModelStateToDictionary(ModelState) });
        }

        _logger.LogInformation("Login POST: Authenticating {UserType} with username: {Username}", model.UserType, model.Username);

        try
        {
            ClaimsIdentity identity;
            string role;
            string displayName;

            if (model.UserType == "Customer")
            {
                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Username == model.Username);
                if (customer == null || !VerifyPassword(model.Password, customer.PasswordHash, customer.Salt))
                {
                    return BadRequest(new { success = false, message = "Tên đăng nhập hoặc mật khẩu không đúng." });
                }

                role = "Customer";
                displayName = customer.Username;

                identity = new ClaimsIdentity(new[]
                {
                new Claim(ClaimTypes.NameIdentifier, customer.CustomerID.ToString()),
                new Claim(ClaimTypes.Name, customer.Username),
                new Claim(ClaimTypes.Role, role)
            }, CookieAuthenticationDefaults.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role);
            }
            else if (model.UserType == "Employee")
            {
                var account = await _context.EmployeeAccounts
                    .Include(ea => ea.Employee)
                    .ThenInclude(e => e.Role)
                    .FirstOrDefaultAsync(ea => ea.Username == model.Username);

                if (account == null || account.Employee == null || account.Employee.Role == null ||
                    !VerifyPassword(model.Password, account.PasswordHash, account.Salt))
                {
                    return BadRequest(new { success = false, message = "Tên đăng nhập hoặc mật khẩu không đúng." });
                }

                //role = account.Employee.Role.RoleName; // e.g., "Admin", "Staff"
                role = "Admin"; // e.g., "Admin", "Staff"
                displayName = account.Username;

                identity = new ClaimsIdentity(new[]
                {
                new Claim(ClaimTypes.NameIdentifier, account.EmployeeID.ToString()),
                new Claim(ClaimTypes.Name, account.Username),
                new Claim(ClaimTypes.Role, role)
            }, CookieAuthenticationDefaults.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role);
            }
            else
            {
                return BadRequest(new { success = false, message = "Loại người dùng không hợp lệ." });
            }

            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
                new AuthenticationProperties { IsPersistent = model.RememberMe });

            _logger.LogInformation("User {Username} signed in with role {Role}", displayName, role);
            return Ok(new { success = true, redirectUrl = Url.Action("Index", "Home") });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login for {Username}", model.Username);
            return StatusCode(500, new { success = false, message = "Lỗi hệ thống. Vui lòng thử lại." });
        }
    }

    // --- [HttpGet] VerifyLoginOtp: Hiển thị form nhập OTP 2FA khi đăng nhập ---
    [AllowAnonymous]
    [HttpGet]
    public IActionResult VerifyLoginOtp()
    {
        var username = TempData["2FA_Attempt_Username"] as string;
        var userType = TempData["2FA_Attempt_UserType"] as string;
        var emailForDisplay = TempData["2FA_Attempt_EmailForDisplay"] as string;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(userType) || string.IsNullOrEmpty(emailForDisplay))
        {
            TempData["ErrorMessage"] = "Phiên xác thực không hợp lệ. Vui lòng đăng nhập lại.";
            _logger.LogWarning("VerifyLoginOtp (GET): Thiếu thông tin TempData.");
            return RedirectToAction("Index", "Home");
        }

        Keep2FATempData(); // Giữ lại TempData

        ViewBag.VerifyingEmail = emailForDisplay;
        return View(new VerifyLoginOtpViewModel { Username = username, UserType = userType, EmailForDisplay = emailForDisplay });
    }

    // --- [HttpPost] VerifyLoginOtp: Xử lý OTP 2FA khi đăng nhập ---
    // --- [HttpPost] VerifyLoginOtp: Xử lý OTP 2FA khi đăng nhập ---
    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyLoginOtp(VerifyLoginOtpViewModel model)
    {
        // Lấy dữ liệu từ TempData
        var originalUsernameFromTemp = TempData["2FA_Attempt_Username"] as string; // Username đã lấy từ DB ở bước 1
        var originalUserTypeFromTemp = TempData["2FA_Attempt_UserType"] as string;
        var emailForDisplayFromTemp = TempData["2FA_Attempt_EmailForDisplay"] as string;
        bool rememberMeFromTemp = TempData["2FA_Attempt_RememberMe"] as bool? ?? false;
        string? roleForClaimsFromTemp = TempData["2FA_Attempt_Role"] as string;
        string? userIdForClaimsFromTemp = TempData["2FA_Attempt_UserId"] as string; // Đây là CustomerID hoặc EmployeeID

        ViewBag.VerifyingEmail = model.EmailForDisplay ?? emailForDisplayFromTemp;
        Keep2FATempData(); // Giữ TempData nếu có lỗi và return View(model)

        // Kiểm tra sự nhất quán giữa model và TempData
        // model.Username từ form OTP, originalUsernameFromTemp là từ DB đã xác thực ở bước 1
        if (!ModelState.IsValid || model.UserType != originalUserTypeFromTemp)
        {
            _logger.LogWarning("VerifyLoginOtp (POST): ModelState không hợp lệ hoặc UserType không khớp TempData. ModelUserType: {ModelUserType}, TempUserType: {TempUserType}",
                                model.UserType, originalUserTypeFromTemp);

            if (model.UserType != originalUserTypeFromTemp)
                ModelState.AddModelError("UserType", "Loại người dùng không khớp với yêu cầu xác thực.");

            // Gán lại giá trị cho ViewModel để View hiển thị đúng nếu lỗi
            model.Username = originalUsernameFromTemp; // Luôn hiển thị username đã xác thực
            model.EmailForDisplay = emailForDisplayFromTemp;
            return View(model);
        }

        OtpRequest? otpRequest = null;
        var otpTypeNameFor2FA = "LoginTwoFactorVerification";

        if (model.UserType == "Customer")
        {
            if (int.TryParse(userIdForClaimsFromTemp, out int customerId))
            {
                _logger.LogInformation("VerifyLoginOtp (Customer): Tìm OTP cho CustomerID: {CustomerId}, OTP Code: {OtpCode}", customerId, model.OtpCode);
                otpRequest = await _context.OtpRequests
                    .Include(or => or.OtpType) // <<== THÊM INCLUDE
                    .Where(or => or.CustomerID == customerId &&
                                 or.OtpCode == model.OtpCode &&
                                 or.OtpType.OtpTypeName == otpTypeNameFor2FA &&
                                 !or.IsUsed &&
                                 or.ExpirationTime > DateTime.UtcNow)
                    .OrderByDescending(or => or.RequestTime) // <<== THÊM ORDER BY
                    .FirstOrDefaultAsync();
            }
        }
        else if (model.UserType == "Employee")
        {
            if (int.TryParse(userIdForClaimsFromTemp, out int employeeIdFromClaims)) // Đây là EmployeeID
            {
                var employeeAccount = await _context.EmployeeAccounts.AsNoTracking()
                                              .FirstOrDefaultAsync(ea => ea.EmployeeID == employeeIdFromClaims);

                if (employeeAccount != null)
                {
                    _logger.LogInformation("VerifyLoginOtp (Employee): Tìm OTP cho EmployeeAccountID: {EmployeeAccountID}, OTP Code: {OtpCode}", employeeAccount.AccountID, model.OtpCode);
                    otpRequest = await _context.OtpRequests
                        .Include(or => or.OtpType) // <<== THÊM INCLUDE
                        .Where(or => or.EmployeeAccountID == employeeAccount.AccountID &&
                                     or.OtpCode == model.OtpCode &&
                                     or.OtpType.OtpTypeName == otpTypeNameFor2FA &&
                                     !or.IsUsed &&
                                     or.ExpirationTime > DateTime.UtcNow)
                        .OrderByDescending(or => or.RequestTime) // <<== THÊM ORDER BY
                        .FirstOrDefaultAsync();
                }
                else { _logger.LogWarning("VerifyLoginOtp (Employee): Không tìm thấy EmployeeAccount cho EmployeeID từ claims: {EmployeeIDFromClaims}", employeeIdFromClaims); }
            }
        }

        if (otpRequest == null)
        {
            ModelState.AddModelError(nameof(model.OtpCode), "Mã OTP không hợp lệ, đã hết hạn hoặc đã được sử dụng.");
            _logger.LogWarning("Login 2FA OTP verification failed. UserType: {UserType}, Username: {Username}, OTP: {OtpCode}, UserIdForClaims: {UserIdForClaims}",
                                model.UserType, originalUsernameFromTemp, model.OtpCode, userIdForClaimsFromTemp);
            return View(model); // TempData đã được Keep ở trên
        }

        otpRequest.IsUsed = true;
        otpRequest.Status = "VerifiedLogin2FA";
        try
        {
            await _context.SaveChangesAsync();

            if (string.IsNullOrEmpty(roleForClaimsFromTemp) || string.IsNullOrEmpty(userIdForClaimsFromTemp) || string.IsNullOrEmpty(originalUsernameFromTemp))
            {
                ModelState.AddModelError(string.Empty, "Lỗi hệ thống: Không thể hoàn tất đăng nhập (thông tin claims bị thiếu).");
                _logger.LogError("VerifyLoginOtp ({UserType}): Dữ liệu claims (Role, UserId, Username) từ TempData bị thiếu. Role: '{Role}', UserId: '{UserId}', Username: '{Username}'",
                                 model.UserType, roleForClaimsFromTemp, userIdForClaimsFromTemp, originalUsernameFromTemp);
                return View(model); // TempData đã được Keep
            }

            // SỬ DỤNG originalUsernameFromTemp (lấy từ DB ở bước 1) để tạo claims
            var identity = CreateClaimsIdentity(originalUsernameFromTemp, roleForClaimsFromTemp, userIdForClaimsFromTemp);
            var principal = new ClaimsPrincipal(identity);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = rememberMeFromTemp,
                ExpiresUtc = rememberMeFromTemp ? DateTimeOffset.UtcNow.AddDays(7) : (DateTimeOffset?)null
            };
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

            _logger.LogInformation("Đăng nhập thành công qua 2FA: UserType={UserType}, Username={Username}, Role={Role}, ID={UserId}, RememberMe={Remember}",
                                   model.UserType, originalUsernameFromTemp, roleForClaimsFromTemp, userIdForClaimsFromTemp, rememberMeFromTemp);

            TempData["SuccessMessage"] = "Đăng nhập thành công!";
            Clear2FATempData();

            return RedirectBasedOnRole(principal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi lưu DB/SignIn sau khi xác minh Login 2FA OTP. UserType: {UserType}, Username: {Username}", model.UserType, originalUsernameFromTemp);
            ModelState.AddModelError(string.Empty, "Lỗi hệ thống khi hoàn tất đăng nhập.");
            return View(model); // TempData đã được Keep
        }
    }

    // --- [HttpPost] Logout ---
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        _logger.LogInformation("User {UserName} logged out.", User.Identity?.Name ?? "Unknown");
        return Ok(new { message = "Đăng xuất thành công.", redirectUrl = Url.Action("Index", "Home") });
    }

    // --- AccessDenied ---
    [AllowAnonymous]
    public IActionResult AccessDenied() { return View(); }

    // === CÁC ACTION CHO ĐĂNG KÝ VÀ XÁC MINH EMAIL KHÁCH HÀNG ===
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectBasedOnRole(User);
        return View(new RegisterViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");

        if (await _context.Customers.AnyAsync(c => c.Username.ToLower() == model.Username.ToLower())) ModelState.AddModelError(nameof(model.Username), "Tên đăng nhập này đã được sử dụng.");
        if (await _context.Customers.AnyAsync(c => c.Email.ToLower() == model.Email.ToLower())) ModelState.AddModelError(nameof(model.Email), "Địa chỉ email này đã được sử dụng.");
        if (await _context.Customers.AnyAsync(c => c.PhoneNumber == model.PhoneNumber)) ModelState.AddModelError(nameof(model.PhoneNumber), "Số điện thoại này đã được sử dụng.");

        if (ModelState.IsValid)
        {
            var customer = new Customer { FirstName = model.FirstName, LastName = model.LastName, Email = model.Email, PhoneNumber = model.PhoneNumber, Username = model.Username, IsEmailVerified = false, ImagePath = "default.jpg" };
            if (model.ProfileImage != null && model.ProfileImage.Length > 0)
            {
                try { var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "users"); if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder); var uniqueFileName = $"{Guid.NewGuid()}{Path.GetExtension(model.ProfileImage.FileName)}"; var filePath = Path.Combine(uploadsFolder, uniqueFileName); using (var fileStream = new FileStream(filePath, FileMode.Create)) { await model.ProfileImage.CopyToAsync(fileStream); } customer.ImagePath = uniqueFileName; }
                catch (Exception ex) { _logger.LogError(ex, "Lỗi tải ảnh KH {Username}.", model.Username); }
            }
            (customer.PasswordHash, customer.Salt) = GeneratePasswordHashAndSalt(model.Password);

            string otpCode = GenerateOtp();
            var otpType = await _context.OtpTypes.FirstOrDefaultAsync(ot => ot.OtpTypeName == "CustomerAccountVerification");
            if (otpType == null) { _logger.LogError("CRITICAL: OtpType 'CustomerAccountVerification' not found."); ModelState.AddModelError(string.Empty, "Lỗi hệ thống (OTP Type)."); return View(model); }
            var otpRequest = new OtpRequest { OtpTypeID = otpType.OtpTypeID, OtpCode = otpCode, RequestTime = DateTime.UtcNow, ExpirationTime = DateTime.UtcNow.AddMinutes(15), IsUsed = false, Status = "PendingVerification" };

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try { _context.Customers.Add(customer); await _context.SaveChangesAsync(); otpRequest.CustomerID = customer.CustomerID; _context.OtpRequests.Add(otpRequest); await _context.SaveChangesAsync(); string emailSubject = "Xác minh tài khoản MiniMart"; string emailMessage = $"<p>Chào {customer.FirstName},</p><p>Mã OTP: <strong>{otpCode}</strong></p><p>Hết hạn sau 15 phút.</p>"; await _emailSender.SendEmailAsync(customer.Email, emailSubject, emailMessage); await transaction.CommitAsync(); _logger.LogInformation("KH {Username} (ID:{Id}) đk. OTP gửi.", customer.Username, customer.CustomerID); TempData["VerificationEmail"] = customer.Email; TempData["InfoMessage"] = "Đăng ký gần xong! Mã OTP đã gửi tới email."; return RedirectToAction(nameof(VerifyOtp)); }
                catch (Exception ex) { await transaction.RollbackAsync(); _logger.LogError(ex, "Lỗi transaction đăng ký KH {Username}.", model.Username); ModelState.AddModelError(string.Empty, "Lỗi đăng ký."); }
            }
        }
        return View(model);
    }
    // === BẮT ĐẦU DÁN CODE SETTINGS (GET và POST) VÀO ĐÂY ===

    // GET: /Account/Settings
    [Authorize(Roles = "Customer")] // Hoặc chỉ [Authorize]
    [HttpGet]
    public async Task<IActionResult> Settings()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int customerId)) { return Challenge(); }
        var customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerID == customerId);
        if (customer == null) { return NotFound("Tài khoản không tồn tại."); }

        var viewModel = new CustomerSettingsViewModel
        { // Đảm bảo bạn có ViewModel này
            CustomerId = customer.CustomerID,
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            Email = customer.Email,
            PhoneNumber = customer.PhoneNumber,
            Username = customer.Username,
            ImagePath = customer.ImagePath
        };
        return View(viewModel);
    }

    // POST: /Account/Settings
    [Authorize(Roles = "Customer")] // Hoặc chỉ [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Settings(CustomerSettingsViewModel model) // ViewModel này phải khớp với class bạn tạo
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int currentUserId) || currentUserId != model.CustomerId)
        {
            _logger.LogWarning("Settings POST: Unauthorized attempt. LoggedInUID: {LoggedInUID}, ModelUID: {ModelUID}", userIdString, model.CustomerId);
            return Forbid();
        }

        var customerInDb = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerID == model.CustomerId);
        if (customerInDb == null)
        {
            return NotFound("Tài khoản không tồn tại để cập nhật.");
        }

        model.Email = customerInDb.Email; // Giữ giá trị Email từ DB
        model.Username = customerInDb.Username; // Giữ giá trị Username từ DB

        ModelState.Remove("Email");
        ModelState.Remove("Username");

        // Thêm kiểm tra CurrentPassword nếu ViewModel không có [Required]
        if (string.IsNullOrEmpty(model.CurrentPassword))
        {
            ModelState.AddModelError("CurrentPassword", "Vui lòng nhập mật khẩu hiện tại để xác nhận.");
        }


        if (ModelState.IsValid)
        {
            if (!VerifyPassword(model.CurrentPassword, customerInDb.PasswordHash, customerInDb.Salt))
            {
                ModelState.AddModelError("CurrentPassword", "Mật khẩu hiện tại không đúng.");
                _logger.LogWarning("Settings POST: Incorrect current password for CustomerID: {CustomerId}", model.CustomerId);
                model.ImagePath = customerInDb.ImagePath; // Gán lại ảnh nếu có lỗi
                return View(model);
            }

            var customerToUpdate = await _context.Customers.FindAsync(model.CustomerId);
            if (customerToUpdate == null) return NotFound("Lỗi: Không tìm thấy khách hàng để cập nhật.");

            customerToUpdate.FirstName = model.FirstName;
            customerToUpdate.LastName = model.LastName;
            customerToUpdate.PhoneNumber = model.PhoneNumber;

            if (model.NewImageFile != null && model.NewImageFile.Length > 0)
            {
                try
                {
                    // ... (logic upload ảnh của bạn đã có ở trên) ...
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "users");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                    if (!string.IsNullOrEmpty(customerToUpdate.ImagePath) && customerToUpdate.ImagePath != "default.jpg")
                    {
                        var oldImagePath = Path.Combine(uploadsFolder, customerToUpdate.ImagePath);
                        if (System.IO.File.Exists(oldImagePath)) System.IO.File.Delete(oldImagePath);
                    }

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(model.NewImageFile.FileName);
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.NewImageFile.CopyToAsync(fileStream);
                    }
                    customerToUpdate.ImagePath = uniqueFileName;
                    // model.ImagePath = uniqueFileName; // Gán cho model để hiển thị lại nếu có lỗi khác sau đó
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi tải ảnh lên cho CustomerID: {CustomerId} trong Settings", model.CustomerId);
                    ModelState.AddModelError("NewImageFile", "Lỗi tải lên ảnh đại diện. Vui lòng thử lại.");
                    model.ImagePath = customerInDb.ImagePath;
                    return View(model);
                }
            }
            else
            {
                customerToUpdate.ImagePath = customerInDb.ImagePath; // Giữ ảnh cũ nếu không có file mới
            }

            try
            {
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Thông tin cá nhân đã được cập nhật thành công!";
                _logger.LogInformation("Settings POST: Thông tin CustomerID {CustomerId} đã được cập nhật.", model.CustomerId);
                // Cập nhật lại thông tin trong originalValues của client-side nếu bạn quay lại trang Profile ngay
                // hoặc đơn giản là redirect để client tự load lại
                return RedirectToAction(nameof(Settings));
            }
            catch (DbUpdateConcurrencyException) { /* ... */ }
            catch (DbUpdateException ex) { /* ... xử lý lỗi UNIQUE PhoneNumber ... */ }
        }

        if (string.IsNullOrEmpty(model.ImagePath) && customerInDb != null)
        { // Gán lại ImagePath nếu có lỗi và model chưa có
            model.ImagePath = customerInDb.ImagePath;
        }
        return View(model);
    }

    // === KẾT THÚC DÁN CODE SETTINGS (POST) VÀO ĐÂY ===
    
    // GET: /Account/ChangePassword
    [Authorize(Roles = "Customer")] // Hoặc chỉ [Authorize] nếu không phân quyền chi tiết
    [HttpGet]
    public IActionResult ChangePassword()
    {
        return View(new ChangePasswordViewModel()); // Truyền ViewModel trống
    }

    // POST: /Account/ChangePassword (Bước 1: Xác thực mk cũ, gửi OTP)
    [Authorize(Roles = "Customer")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int customerId))
        {
            _logger.LogWarning("ChangePassword POST: User not authenticated or CustomerID claim is missing.");
            return Unauthorized("Không thể xác định người dùng."); // Hoặc Challenge()
        }

        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.CustomerID == customerId);
        if (customer == null)
        {
            _logger.LogError("ChangePassword POST: Customer with ID {CustomerId} not found in DB.", customerId);
            return NotFound("Tài khoản không tồn tại.");
        }

        // 1. Xác thực mật khẩu hiện tại
        if (!VerifyPassword(model.CurrentPassword, customer.PasswordHash, customer.Salt))
        {
            ModelState.AddModelError("CurrentPassword", "Mật khẩu hiện tại không đúng.");
            _logger.LogWarning("ChangePassword POST: Incorrect current password for CustomerID {CustomerId}.", customerId);
            return View(model);
        }

        // 2. Tạo và gửi OTP
        var otpTypeName = "UserChangePasswordVerification"; // Khớp với OtpTypes trong DB
        var otpType = await _context.OtpTypes.FirstOrDefaultAsync(ot => ot.OtpTypeName == otpTypeName);
        if (otpType == null)
        {
            _logger.LogError("CRITICAL: OtpType '{OtpTypeName}' not found for Change Password.", otpTypeName);
            ModelState.AddModelError(string.Empty, "Lỗi hệ thống: Không thể xử lý yêu cầu đổi mật khẩu (OTP Type).");
            return View(model);
        }

        // Vô hiệu hóa OTP cùng loại chưa sử dụng của khách hàng này (nếu có)
        var existingOtps = await _context.OtpRequests
            .Where(o => o.CustomerID == customerId && o.OtpTypeID == otpType.OtpTypeID && !o.IsUsed && o.ExpirationTime > DateTime.UtcNow)
            .ToListAsync();
        foreach (var oldOtp in existingOtps)
        {
            oldOtp.IsUsed = true;
            oldOtp.Status = "InvalidatedByNewRequest";
        }

        string otpCode = GenerateOtp();
        var otpRequest = new OtpRequest
        {
            CustomerID = customer.CustomerID,
            EmployeeAccountID = null, // Vì đây là Customer
            OtpTypeID = otpType.OtpTypeID,
            OtpCode = otpCode,
            RequestTime = DateTime.UtcNow,
            ExpirationTime = DateTime.UtcNow.AddMinutes(10), // OTP hết hạn sau 10 phút
            IsUsed = false,
            Status = "PendingPasswordChangeConfirm"
        };
        _context.OtpRequests.Add(otpRequest);

        try
        {
            await _context.SaveChangesAsync(); // Lưu OTP và các OTP cũ đã vô hiệu hóa
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lưu OTP Request cho đổi mật khẩu của CustomerID {CustomerId}", customerId);
            ModelState.AddModelError(string.Empty, "Lỗi hệ thống khi tạo yêu cầu OTP.");
            return View(model);
        }

        // Lưu mật khẩu MỚI (đã được validate bởi ViewModel) vào TempData để dùng sau khi OTP được xác nhận.
        // CẢNH BÁO: Đây là cách đơn giản, không phải là an toàn nhất cho mật khẩu clear text.
        // Trong môi trường production, hãy cân nhắc mã hóa TempData hoặc dùng cơ chế khác an toàn hơn.
        TempData["PendingChange_NewPassword"] = model.NewPassword;
        TempData["PendingChange_CustomerId"] = customer.CustomerID.ToString(); // Lưu ID để xác thực ở bước OTP


        string emailSubject = "Xác nhận yêu cầu thay đổi mật khẩu MiniMart";
        string emailMessage = $@"
            <p>Xin chào {customer.FirstName},</p>
            <p>Chúng tôi nhận được yêu cầu thay đổi mật khẩu cho tài khoản MiniMart của bạn.</p>
            <p>Mã OTP để xác nhận việc thay đổi mật khẩu của bạn là: <strong>{otpCode}</strong></p>
            <p>Mã này sẽ hết hạn sau 10 phút.</p>
            <p>Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email này và xem xét việc bảo mật tài khoản của bạn.</p>";

        await _emailSender.SendEmailAsync(customer.Email, emailSubject, emailMessage);
        _logger.LogInformation("ChangePassword POST: OTP for password change sent to CustomerID {CustomerId} at email {CustomerEmail}", customer.CustomerID, customer.Email);

        // Chuyển hướng đến trang nhập OTP
        return RedirectToAction(nameof(VerifyOtpForAction), new
        {
            purpose = "ChangePassword",
            detail = $"Một mã OTP đã được gửi đến email <strong>{customer.Email}</strong>. Vui lòng nhập mã để hoàn tất việc đổi mật khẩu."
        });
    }

    [Authorize(Roles = "Customer")] // Hoặc chỉ [Authorize]
    [HttpPost] // <<<<===== QUAN TRỌNG: Đây là action cho POST
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestChangeEmail(RequestChangeEmailViewModel model) // model sẽ được binding từ form
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int customerId))
        {
            _logger.LogWarning("RequestChangeEmail POST: User not authenticated or CustomerID claim is missing.");
            return Challenge(); // Hoặc một lỗi phù hợp hơn
        }

        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.CustomerID == customerId);
        if (customer == null)
        {
            _logger.LogWarning("RequestChangeEmail POST: Customer with ID {CustomerId} not found for user.", customerId);
            return NotFound("Tài khoản không tồn tại.");
        }

        // Gán lại CurrentEmail để hiển thị đúng trên View nếu có lỗi và return View(model)
        model.CurrentEmail = customer.Email;

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("RequestChangeEmail POST: ModelState is invalid for CustomerID {CustomerId}.", customerId);
            return View(model); // Trả về view với các lỗi validation
        }

        // 1. Xác thực mật khẩu hiện tại của người dùng
        if (!VerifyPassword(model.CurrentPassword, customer.PasswordHash, customer.Salt)) // Hàm VerifyPassword của bạn
        {
            ModelState.AddModelError("CurrentPassword", "Mật khẩu hiện tại không đúng.");
            _logger.LogWarning("RequestChangeEmail POST: Incorrect current password for CustomerID {CustomerId}.", customerId);
            return View(model);
        }

        // 2. Kiểm tra Email mới
        var newEmailNormalized = model.NewEmail.Trim().ToLower(); // Chuẩn hóa email mới
        if (string.Equals(newEmailNormalized, customer.Email.ToLower(), StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError("NewEmail", "Email mới phải khác với email hiện tại.");
            return View(model);
        }
        // Kiểm tra xem email mới đã được sử dụng bởi tài khoản khác chưa (ngoại trừ chính user này)
        if (await _context.Customers.AnyAsync(c => c.Email.ToLower() == newEmailNormalized && c.CustomerID != customerId))
        {
            ModelState.AddModelError("NewEmail", "Địa chỉ email này đã được sử dụng bởi một tài khoản khác.");
            _logger.LogWarning("RequestChangeEmail POST: New email {NewEmail} is already in use by another account. CustomerID {CustomerId}", newEmailNormalized, customerId);
            return View(model);
        }

        // 3. Tạo và gửi OTP đến ĐỊA CHỈ EMAIL MỚI
        var otpTypeName = "CustomerChangeEmailVerification"; // Đảm bảo OtpType này tồn tại trong DB
        var otpType = await _context.OtpTypes.FirstOrDefaultAsync(ot => ot.OtpTypeName == otpTypeName);
        if (otpType == null)
        {
            _logger.LogError("CRITICAL: OtpType '{OtpTypeName}' not found for Change Email verification.", otpTypeName);
            ModelState.AddModelError(string.Empty, "Lỗi hệ thống: Không thể xử lý yêu cầu thay đổi email (OTP Config).");
            return View(model);
        }

        // Vô hiệu hóa các OTP cùng loại, cùng CustomerID chưa được sử dụng trước đó
        var existingUnusedOtps = await _context.OtpRequests
            .Where(o => o.CustomerID == customerId &&
                        o.OtpTypeID == otpType.OtpTypeID &&
                        !o.IsUsed &&
                        o.ExpirationTime > DateTime.UtcNow)
            .ToListAsync();
        foreach (var oldOtp in existingUnusedOtps)
        {
            oldOtp.IsUsed = true;
            oldOtp.Status = "InvalidatedByNewEmailChangeRequest"; // Trạng thái mới
        }

        string otpCode = GenerateOtp(); // Hàm GenerateOtp của bạn
        var otpRequest = new OtpRequest
        {
            CustomerID = customer.CustomerID,
            EmployeeAccountID = null,
            OtpTypeID = otpType.OtpTypeID,
            OtpCode = otpCode,
            RequestTime = DateTime.UtcNow,
            ExpirationTime = DateTime.UtcNow.AddMinutes(15),
            IsUsed = false,
            Status = $"PendingChangeEmailTo:{newEmailNormalized}" // Lưu email mới để xác minh ở bước sau
        };
        _context.OtpRequests.Add(otpRequest);

        try
        {
            await _context.SaveChangesAsync(); // Lưu các thay đổi (OTP mới và các OTP cũ bị vô hiệu hóa)
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lưu OtpRequest cho việc thay đổi email của CustomerID {CustomerId}", customerId);
            ModelState.AddModelError(string.Empty, "Lỗi hệ thống khi tạo yêu cầu OTP. Vui lòng thử lại.");
            return View(model);
        }

        // Lưu thông tin cần thiết vào TempData để action VerifyOtpForAction có thể sử dụng
        TempData["PendingNewEmail_ForVerification"] = newEmailNormalized;
        TempData["PendingChangeEmail_CustomerId"] = customer.CustomerID.ToString();

        // Chuẩn bị email để gửi OTP
        string emailSubject = "Xác minh yêu cầu thay đổi địa chỉ Email cho tài khoản MiniMart";
        string emailMessage = $@"
        <p>Xin chào,</p>
        <p>Chúng tôi nhận được yêu cầu thay đổi địa chỉ email liên kết với tài khoản MiniMart (Tên đăng nhập: {customer.Username}) thành địa chỉ email này (<strong>{newEmailNormalized}</strong>).</p>
        <p>Để xác nhận rằng bạn là chủ sở hữu của địa chỉ email mới này và đồng ý với thay đổi, vui lòng sử dụng mã OTP sau:</p>
        <p style='font-size: 1.5em; font-weight: bold; text-align: center;'>{otpCode}</p>
        <p>Mã này sẽ hết hạn sau 15 phút.</p>
        <p>Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email này và KHÔNG chia sẻ mã OTP này cho bất kỳ ai.</p>
        <p>Trân trọng,<br/>Đội ngũ MiniMart</p>";

        // GỬI EMAIL ĐẾN ĐỊA CHỈ EMAIL MỚI (newEmailNormalized)
        await _emailSender.SendEmailAsync(newEmailNormalized, emailSubject, emailMessage);
        _logger.LogInformation("RequestChangeEmail POST: OTP for new email verification sent to {NewEmail} for CustomerID {CustomerId}", newEmailNormalized, customer.CustomerID);

        string successDetail = $"Một mã OTP đã được gửi đến <strong>{newEmailNormalized}</strong>. Vui lòng kiểm tra hộp thư (cả Spam/Junk) và nhập mã OTP để hoàn tất.";
        TempData["InfoMessage"] = successDetail; // Thông báo cho người dùng trên trang OTP

        // Chuyển hướng đến trang nhập OTP dùng chung
        return RedirectToAction(nameof(VerifyOtpForAction), new
        {
            purpose = "ChangeEmail_NewEmailVerification",
            detail = successDetail // Truyền thông báo qua query string để View hiển thị
        });
    }
    [Authorize(Roles = "Customer")] // Hoặc chỉ [Authorize]
    [HttpGet] // Quan trọng: Đảm bảo có [HttpGet]
    public async Task<IActionResult> RequestChangeEmail()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int customerId))
        {
            _logger.LogWarning("RequestChangeEmail GET: User not authenticated or CustomerID claim is missing.");
            return Challenge(); // Hoặc Unauthorized()
        }

        var customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerID == customerId);
        if (customer == null)
        {
            _logger.LogWarning("RequestChangeEmail GET: Customer with ID {CustomerId} not found.", customerId);
            return NotFound("Tài khoản không tồn tại.");
        }

        // Truyền CurrentEmail để hiển thị trên View
        var viewModel = new RequestChangeEmailViewModel
        {
            CurrentEmail = customer.Email
        };

        _logger.LogInformation("RequestChangeEmail GET: Displaying form for CustomerID {CustomerId}", customerId);
        return View(viewModel);
    }
    [Authorize(Roles = "Admin,Staff")] // Chỉ Admin và Staff mới xem được profile của mình
    [HttpGet]
    public async Task<IActionResult> EmployeeProfile()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier); // Đây là EmployeeID từ claims
        if (!int.TryParse(userIdString, out int employeeIdFromClaims))
        {
            _logger.LogWarning("EmployeeProfile GET: Không thể parse EmployeeID từ claims.");
            return Challenge();
        }

        var employee = await _context.Employees
            .Include(e => e.Role)
            .Include(e => e.EmployeeAccount)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EmployeeID == employeeIdFromClaims);

        if (employee == null)
        {
            _logger.LogWarning("EmployeeProfile GET: Nhân viên với ID {EmployeeId} không tìm thấy.", employeeIdFromClaims);
            return NotFound("Không tìm thấy thông tin nhân viên.");
        }

        var viewModel = new EmployeeProfileViewModel // Sử dụng EmployeeProfileViewModel
        {
            EmployeeId = employee.EmployeeID,
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            Email = employee.Email,
            PhoneNumber = employee.PhoneNumber,
            Gender = employee.Gender,
            BirthDate = employee.BirthDate,
            CitizenID = employee.CitizenID,
            HireDate = employee.HireDate,
            RoleName = employee.Role?.RoleName ?? "N/A",
            ImagePath = employee.ImagePath,
            Username = employee.EmployeeAccount?.Username ?? "N/A",
            IsAccountActive = employee.EmployeeAccount?.IsActive ?? false,
            IsEmployeeEmailVerified = employee.EmployeeAccount?.IsEmailVerified ?? false, // Lấy từ bảng EmployeeAccounts
            EmployeeEmailVerifiedAt = employee.EmployeeAccount?.EmailVerifiedAt  // Lấy từ bảng EmployeeAccounts
        };

        return View("EmployeeProfile", viewModel); // Chỉ định tên View
    }

    // POST: /Account/EmployeeProfile (Xử lý cập nhật thông tin cho nhân viên)
    [Authorize(Roles = "Admin,Staff")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EmployeeProfile(EmployeeProfileViewModel model)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier); // EmployeeID từ claims
        if (!int.TryParse(userIdString, out int currentEmployeeId) || currentEmployeeId != model.EmployeeId)
        {
            _logger.LogWarning("EmployeeProfile POST: Unauthorized attempt or ID mismatch. LoggedInEID: {LoggedInEID}, ModelEID: {ModelEID}",
                                userIdString, model.EmployeeId);
            return Forbid("Bạn không có quyền thực hiện hành động này.");
        }

        // Lấy thông tin Employee và EmployeeAccount gốc từ DB để so sánh và xác thực
        var employeeInDb = await _context.Employees
                                .Include(e => e.EmployeeAccount) // Cần EmployeeAccount để xác thực mật khẩu
                                .FirstOrDefaultAsync(e => e.EmployeeID == model.EmployeeId);

        if (employeeInDb == null || employeeInDb.EmployeeAccount == null)
        {
            _logger.LogError("EmployeeProfile POST: Employee hoặc EmployeeAccount không tìm thấy cho ID: {EmployeeId}", model.EmployeeId);
            return NotFound("Tài khoản nhân viên không tồn tại hoặc thiếu thông tin tài khoản đăng nhập.");
        }

        // Gán lại các giá trị không được phép sửa từ form vào model để View hiển thị đúng nếu có lỗi
        model.Email = employeeInDb.Email;
        model.Username = employeeInDb.EmployeeAccount.Username;
        model.RoleName = (await _context.EmployeeRoles.AsNoTracking().FirstOrDefaultAsync(r => r.RoleID == employeeInDb.RoleID))?.RoleName ?? "N/A";
        model.Gender = employeeInDb.Gender;
        model.BirthDate = employeeInDb.BirthDate;
        model.CitizenID = employeeInDb.CitizenID;
        model.HireDate = employeeInDb.HireDate;
        model.IsAccountActive = employeeInDb.EmployeeAccount.IsActive;
        model.IsEmployeeEmailVerified = employeeInDb.EmployeeAccount.IsEmailVerified;
        model.EmployeeEmailVerifiedAt = employeeInDb.EmployeeAccount.EmailVerifiedAt;
        // Giữ ImagePath cũ nếu không có ảnh mới được tải lên và có lỗi xảy ra
        if (string.IsNullOrEmpty(model.ImagePath) && model.NewImageFile == null) model.ImagePath = employeeInDb.ImagePath;


        // === VALIDATION AND UPDATE LOGIC ===
        bool infoAttemptedToChange = (employeeInDb.PhoneNumber != model.PhoneNumber || model.NewImageFile != null);

        // Chỉ yêu cầu mật khẩu nếu người dùng thực sự cố gắng thay đổi các trường được phép
        if (infoAttemptedToChange && string.IsNullOrEmpty(model.CurrentPasswordForUpdate))
        {
            ModelState.AddModelError("CurrentPasswordForUpdate", "Vui lòng nhập mật khẩu hiện tại để xác nhận thay đổi.");
        }
        // Các validation khác (ví dụ: PhoneNumber) đã được xử lý bởi DataAnnotations trên ViewModel

        if (ModelState.IsValid)
        {
            // Nếu có ý định thay đổi, phải xác thực mật khẩu
            if (infoAttemptedToChange)
            {
                if (!VerifyPassword(model.CurrentPasswordForUpdate!, employeeInDb.EmployeeAccount.PasswordHash, employeeInDb.EmployeeAccount.Salt))
                {
                    ModelState.AddModelError("CurrentPasswordForUpdate", "Mật khẩu hiện tại không đúng.");
                    _logger.LogWarning("EmployeeProfile POST: Incorrect current password for EmployeeID: {EmployeeId}", model.EmployeeId);
                    model.ImagePath = employeeInDb.ImagePath; // Giữ lại ảnh cũ khi hiển thị lại form
                    return View("EmployeeProfile", model);
                }
            }

            bool actualChangesMadeToDb = false;

            // Cập nhật Số điện thoại nếu có thay đổi
            if (employeeInDb.PhoneNumber != model.PhoneNumber)
            {
                // Kiểm tra UNIQUE cho PhoneNumber (nếu nó là UNIQUE trong bảng Employees và Customers)
                if (await _context.Employees.AnyAsync(e => e.PhoneNumber == model.PhoneNumber && e.EmployeeID != model.EmployeeId) ||
                    await _context.Customers.AnyAsync(c => c.PhoneNumber == model.PhoneNumber)) // Giả sử SĐT của NV và KH không được trùng
                {
                    ModelState.AddModelError("PhoneNumber", "Số điện thoại này đã được sử dụng.");
                    model.ImagePath = employeeInDb.ImagePath; // Gán lại ảnh trước khi return
                    return View("EmployeeProfile", model);
                }
                employeeInDb.PhoneNumber = model.PhoneNumber;
                actualChangesMadeToDb = true;
                _logger.LogInformation("EmployeeProfile POST: PhoneNumber updated for EID {EmployeeId}", model.EmployeeId);
            }

            // Xử lý upload ảnh đại diện mới
            if (model.NewImageFile != null && model.NewImageFile.Length > 0)
            {
                try
                {
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "employees"); // Thư mục riêng
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                    if (!string.IsNullOrEmpty(employeeInDb.ImagePath) && employeeInDb.ImagePath != "default_employee.jpg") // Ảnh mặc định
                    {
                        var oldImagePath = Path.Combine(uploadsFolder, employeeInDb.ImagePath);
                        if (System.IO.File.Exists(oldImagePath)) System.IO.File.Delete(oldImagePath);
                    }
                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(model.NewImageFile.FileName);
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.NewImageFile.CopyToAsync(fileStream);
                    }
                    employeeInDb.ImagePath = uniqueFileName;
                    model.ImagePath = uniqueFileName; // Cập nhật model để View thấy ảnh mới nếu có lỗi sau đó
                    actualChangesMadeToDb = true;
                    _logger.LogInformation("EmployeeProfile POST: ImagePath updated for EID {EmployeeId} to {NewImage}", model.EmployeeId, uniqueFileName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi upload ảnh EmployeeProfile cho EID: {EmployeeId}", model.EmployeeId);
                    ModelState.AddModelError("NewImageFile", "Lỗi tải lên ảnh đại diện.");
                    model.ImagePath = employeeInDb.ImagePath; // Quay lại ảnh cũ
                    return View("EmployeeProfile", model);
                }
            }

            if (actualChangesMadeToDb)
            {
                try
                {
                    // Entity employeeInDb đã được theo dõi bởi context từ lần FindAsync trước (nếu không dùng AsNoTracking)
                    // hoặc dùng Update nếu đã AsNoTracking ở lần lấy employeeInDb ban đầu.
                    // Vì ta cần EmployeeAccount cho VerifyPassword, ta lấy employeeInDb với tracking ngay từ đầu.
                    _context.Employees.Update(employeeInDb);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Thông tin cá nhân của bạn đã được cập nhật thành công!";
                    _logger.LogInformation("EmployeeProfile POST: Thông tin EmployeeID {EmployeeId} đã được cập nhật vào DB.", model.EmployeeId);
                    return RedirectToAction(nameof(EmployeeProfile));
                }
                catch (DbUpdateException exDb)
                {
                    _logger.LogError(exDb, "EmployeeProfile POST: Lỗi DB khi cập nhật EID: {EmployeeId}. Inner: {Inner}", model.EmployeeId, exDb.InnerException?.Message);
                    ModelState.AddModelError("", "Lỗi cập nhật cơ sở dữ liệu. Vui lòng thử lại.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "EmployeeProfile POST: Lỗi không xác định khi cập nhật EID: {EmployeeId}", model.EmployeeId);
                    ModelState.AddModelError("", "Đã có lỗi xảy ra. Vui lòng thử lại.");
                }
            }
            else if (infoAttemptedToChange && !actualChangesMadeToDb)
            {
                // Trường hợp này ít xảy ra nếu logic trên đúng (ví dụ: SĐT nhập vào giống hệt SĐT cũ)
                TempData["InfoMessage"] = "Không có thông tin nào thực sự thay đổi.";
                return RedirectToAction(nameof(EmployeeProfile));
            }
            else if (!infoAttemptedToChange)
            {
                // Người dùng submit form mà không sửa gì, chỉ nhập mật khẩu (hoặc không)
                TempData["InfoMessage"] = "Không có thông tin nào được gửi để thay đổi.";
                return RedirectToAction(nameof(EmployeeProfile));
            }
        }

        // Nếu ModelState không hợp lệ từ đầu, hoặc có lỗi trong quá trình xử lý
        // Đảm bảo ImagePath được gán lại cho model để View hiển thị đúng ảnh
        if (string.IsNullOrEmpty(model.ImagePath) && employeeInDb != null)
        {
            model.ImagePath = employeeInDb.ImagePath;
        }
        return View("EmployeeProfile", model);
    }
    // Action VerifyOtpForAction (GET) đã được phác thảo ở phản hồi trước
    // GET: /Account/VerifyOtpForAction
    // GET: /Account/VerifyOtpForAction
    [Authorize(Roles = "Customer")]
    [HttpGet]
    public IActionResult VerifyOtpForAction(string purpose, string? detail)
    {
        if (string.IsNullOrEmpty(purpose))
        {
            _logger.LogWarning("VerifyOtpForAction GET: Purpose is missing.");
            return BadRequest("Mục đích xác thực không được cung cấp.");
        }

        bool isValidSession = false;
        string currentEmailForDisplay = User.FindFirstValue(ClaimTypes.Email) ?? "email của bạn"; // Lấy email hiện tại

        if (purpose == "ChangePassword")
        {
            if (TempData["PendingChange_NewPassword"] != null && TempData["PendingChange_CustomerId"] != null)
            {
                isValidSession = true;
                TempData.Keep("PendingChange_NewPassword");
                TempData.Keep("PendingChange_CustomerId");
                // Nếu detail rỗng, tạo detail mặc định
                if (string.IsNullOrEmpty(detail))
                    detail = $"Một mã OTP đã được gửi đến {currentEmailForDisplay} để xác nhận thay đổi mật khẩu.";
            }
        }
        else if (purpose == "ChangeEmail_NewEmailVerification")
        {
            if (TempData["PendingNewEmail_ForVerification"] != null && TempData["PendingChangeEmail_CustomerId"] != null)
            {
                isValidSession = true;
                string? pendingNewEmail = TempData["PendingNewEmail_ForVerification"] as string;
                // Nếu detail rỗng, tạo detail mặc định
                if (string.IsNullOrEmpty(detail) && !string.IsNullOrEmpty(pendingNewEmail))
                    detail = $"Một mã OTP đã được gửi đến địa chỉ email mới <strong>{pendingNewEmail}</strong> để xác minh. Vui lòng kiểm tra và nhập mã OTP.";

                TempData.Keep("PendingNewEmail_ForVerification");
                TempData.Keep("PendingChangeEmail_CustomerId");
            }
        }
        // Thêm các case 'else if' cho các 'purpose' khác của bạn nếu có

        if (!isValidSession)
        {
            _logger.LogWarning("VerifyOtpForAction GET: Invalid session for purpose '{Purpose}'. Redirecting.", purpose);
            TempData["ErrorMessage"] = "Phiên làm việc không hợp lệ hoặc đã hết hạn. Vui lòng thử lại từ đầu.";
            // Điều hướng cụ thể dựa trên purpose
            if (purpose == "ChangePassword") return RedirectToAction(nameof(ChangePassword));
            if (purpose == "ChangeEmail_NewEmailVerification") return RedirectToAction(nameof(RequestChangeEmail));
            return RedirectToAction("Index", "Home"); // Fallback chung
        }

        TempData["LastVerificationDetail"] = detail; // Lưu lại detail để dùng nếu postback có lỗi

        var viewModel = new VerifyOtpGeneralViewModel
        {
            Purpose = purpose,
            VerificationDetail = detail // Truyền detail đã được cập nhật (nếu có)
        };

        _logger.LogInformation("VerifyOtpForAction GET: Displaying OTP form for purpose '{Purpose}'. Detail: {Detail}", purpose, detail);
        return View("VerifyOtpGeneral", viewModel);
    }


    // POST: /Account/VerifyOtpForAction
    // Trong AccountController.cs

    [Authorize(Roles = "Customer")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOtpForAction(VerifyOtpGeneralViewModel model)
    {
        if (string.IsNullOrEmpty(model.VerificationDetail) && TempData["LastVerificationDetail"] != null)
        {
            model.VerificationDetail = TempData["LastVerificationDetail"] as string;
        }
        TempData.Keep("LastVerificationDetail");

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("VerifyOtpForAction POST: ModelState invalid. Purpose: {Purpose}", model.Purpose);
            if (model.Purpose == "ChangePassword") { TempData.Keep("PendingChange_NewPassword"); TempData.Keep("PendingChange_CustomerId"); }
            if (model.Purpose == "ChangeEmail_NewEmailVerification") { TempData.Keep("PendingNewEmail_ForVerification"); TempData.Keep("PendingChangeEmail_CustomerId"); }
            return View("VerifyOtpGeneral", model);
        }

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int currentUserId))
        {
            _logger.LogError("VerifyOtpForAction POST: Cannot parse currentUserId from claims.");
            ModelState.AddModelError(string.Empty, "Lỗi xác thực người dùng.");
            return View("VerifyOtpGeneral", model);
        }

        // --- Xử lý cho Đổi Mật khẩu ---
        if (model.Purpose == "ChangePassword")
        {
            string? tempCustomerIdStr = TempData["PendingChange_CustomerId"] as string;
            var newPasswordFromTemp = TempData["PendingChange_NewPassword"] as string;

            if (!int.TryParse(tempCustomerIdStr, out int targetCustId) || targetCustId != currentUserId || string.IsNullOrEmpty(newPasswordFromTemp))
            {
                ModelState.AddModelError("", "Phiên đổi mật khẩu không hợp lệ hoặc đã hết hạn. Vui lòng thử lại.");
                return View("VerifyOtpGeneral", model);
            }
            // Giữ lại TempData nếu OTP sai để thử lại
            TempData.Keep("PendingChange_NewPassword"); TempData.Keep("PendingChange_CustomerId");

            var otpTypeNameForPassword = "UserChangePasswordVerification";
            var otpRequest = await _context.OtpRequests
                .Include(or => or.OtpType)
                .Where(or => or.CustomerID == currentUserId &&
                             or.OtpCode == model.OtpCode &&
                             or.OtpType.OtpTypeName == otpTypeNameForPassword &&
                             !or.IsUsed &&
                             or.ExpirationTime > DateTime.UtcNow)
                .OrderByDescending(or => or.RequestTime)
                .FirstOrDefaultAsync();

            if (otpRequest == null)
            {
                ModelState.AddModelError("OtpCode", "Mã OTP không hợp lệ, đã hết hạn hoặc đã được sử dụng.");
                return View("VerifyOtpGeneral", model);
            }

            var customer = await _context.Customers.FindAsync(currentUserId);
            if (customer == null) { _logger.LogError("Customer {CustomerId} not found after valid OTP for password change.", currentUserId); return NotFound("Lỗi: Tài khoản không tìm thấy."); }

            (customer.PasswordHash, customer.Salt) = GeneratePasswordHashAndSalt(newPasswordFromTemp);
            otpRequest.IsUsed = true;
            otpRequest.Status = "VerifiedAndChangedPassword";
            try
            {
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Mật khẩu của bạn đã được thay đổi thành công!";
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                TempData["InfoMessage"] = "Vui lòng đăng nhập lại với mật khẩu mới.";
                TempData.Remove("PendingChange_NewPassword"); TempData.Remove("PendingChange_CustomerId"); TempData.Remove("LastVerificationDetail");
                return RedirectToAction(nameof(Login));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi lưu DB sau khi đổi mật khẩu cho KH {CustomerId}", currentUserId);
                ModelState.AddModelError("", "Lỗi hệ thống khi cập nhật mật khẩu.");
            }
            return View("VerifyOtpGeneral", model);
        }
        // --- Xử lý cho Thay đổi Email ---
        else if (model.Purpose == "ChangeEmail_NewEmailVerification")
        {
            string? tempCustomerIdStr = TempData["PendingChangeEmail_CustomerId"] as string;
            var pendingNewEmail = TempData["PendingNewEmail_ForVerification"] as string;

            if (!int.TryParse(tempCustomerIdStr, out int targetCustomerIdFromTemp) || targetCustomerIdFromTemp != currentUserId || string.IsNullOrEmpty(pendingNewEmail))
            {
                ModelState.AddModelError(string.Empty, "Phiên thay đổi email không hợp lệ. Vui lòng yêu cầu lại.");
                return View("VerifyOtpGeneral", model);
            }
            TempData.Keep("PendingNewEmail_ForVerification"); TempData.Keep("PendingChangeEmail_CustomerId");

            var otpTypeNameForEmail = "CustomerChangeEmailVerification";
            string expectedStatus = $"pendingchangeemailto:{pendingNewEmail.ToLowerInvariant()}";

            var otpRequest = await _context.OtpRequests
                .Include(or => or.OtpType)
                .Where(or => or.CustomerID == currentUserId &&
                             or.OtpCode == model.OtpCode &&
                             or.OtpType.OtpTypeName == otpTypeNameForEmail &&
                             !or.IsUsed &&
                             or.ExpirationTime > DateTime.UtcNow &&
                             or.Status != null &&
                             or.Status.ToLower() == expectedStatus) // Đã sửa
                .OrderByDescending(or => or.RequestTime)
                .FirstOrDefaultAsync();

            if (otpRequest == null)
            {
                ModelState.AddModelError("OtpCode", "Mã OTP không hợp lệ, hết hạn, đã dùng, hoặc không đúng cho email này.");
                return View("VerifyOtpGeneral", model);
            }

            var customerToUpdate = await _context.Customers.FindAsync(currentUserId);
            if (customerToUpdate == null) { return NotFound("Lỗi: Tài khoản không tìm thấy."); }

            if (await _context.Customers.AnyAsync(c => c.Email.ToLower() == pendingNewEmail.ToLowerInvariant() && c.CustomerID != currentUserId))
            {
                TempData["ErrorMessage"] = $"Không thể cập nhật. Email '{pendingNewEmail}' đã được tài khoản khác sử dụng.";
                otpRequest.IsUsed = true; otpRequest.Status = "InvalidatedEmailConflictOnVerify"; await _context.SaveChangesAsync();
                TempData.Remove("PendingNewEmail_ForVerification"); TempData.Remove("PendingChangeEmail_CustomerId"); TempData.Remove("LastVerificationDetail");
                return RedirectToAction(nameof(RequestChangeEmail));
            }

            var oldEmail = customerToUpdate.Email;
            customerToUpdate.Email = pendingNewEmail;

            bool wasEmailVerificationReset = false;
            var isEmailVerifiedPropInfo = _context.Model.FindEntityType(typeof(Customer))?.FindProperty("IsEmailVerified");
            if (isEmailVerifiedPropInfo != null) { customerToUpdate.IsEmailVerified = false; wasEmailVerificationReset = true; }
            var emailVerifiedAtPropInfo = _context.Model.FindEntityType(typeof(Customer))?.FindProperty("EmailVerifiedAt");
            if (emailVerifiedAtPropInfo != null) { customerToUpdate.EmailVerifiedAt = null; }

            otpRequest.IsUsed = true; otpRequest.Status = "VerifiedAndEmailUpdated";

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _context.SaveChangesAsync();
                string successMsg = $"Địa chỉ email đã đổi thành <strong>{pendingNewEmail}</strong>.";
                try { await _emailSender.SendEmailAsync(oldEmail, "Thông báo thay đổi email MiniMart", $"Email tài khoản MiniMart của bạn đã đổi từ {oldEmail} sang {pendingNewEmail}."); }
                catch (Exception exMailOld) { _logger.LogError(exMailOld, "Lỗi gửi mail báo đến email cũ {OldEmail}", oldEmail); }

                if (wasEmailVerificationReset)
                {
                    var verificationOtpType = await _context.OtpTypes.FirstOrDefaultAsync(ot => ot.OtpTypeName == "CustomerAccountVerification");
                    if (verificationOtpType != null)
                    {
                        string newVerificationOtpCode = GenerateOtp();
                        var newVerificationOtp = new OtpRequest { CustomerID = currentUserId, OtpTypeID = verificationOtpType.OtpTypeID, OtpCode = newVerificationOtpCode, RequestTime = DateTime.UtcNow, ExpirationTime = DateTime.UtcNow.AddHours(24), IsUsed = false, Status = $"ReVerifyNewEmail:{pendingNewEmail}" };
                        _context.OtpRequests.Add(newVerificationOtp);
                        await _context.SaveChangesAsync();
                        await _emailSender.SendEmailAsync(pendingNewEmail, "Xác minh email mới tại MiniMart", $"Mã OTP để xác minh email mới ({pendingNewEmail}) là: <strong>{newVerificationOtpCode}</strong>.");
                        successMsg += " Một OTP đã gửi đến email mới để bạn xác minh lại.";
                    }
                }
                await transaction.CommitAsync();
                TempData["SuccessMessage"] = successMsg;
                TempData.Remove("PendingNewEmail_ForVerification"); TempData.Remove("PendingChangeEmail_CustomerId"); TempData.Remove("LastVerificationDetail");
                return RedirectToAction(nameof(Settings));
            }
            catch (DbUpdateException dbEx) { await transaction.RollbackAsync(); /* ... xử lý lỗi DB ... */ ModelState.AddModelError("", "Lỗi cập nhật email (DB)."); }
            catch (Exception exGen) { await transaction.RollbackAsync(); /* ... log lỗi chung ... */ ModelState.AddModelError("", "Lỗi hệ thống khi cập nhật email."); }

            return View("VerifyOtpGeneral", model);
        }
        else
        {
            ModelState.AddModelError(string.Empty, "Yêu cầu không hợp lệ.");
        }
        return View("VerifyOtpGeneral", model);
    }
    [AllowAnonymous]
    [HttpGet]
    public IActionResult VerifyOtp() // Dùng cho xác minh email khi đăng ký
    {
        var email = TempData["VerificationEmail"] as string;
        if (email != null) TempData.Keep("VerificationEmail");
        if (string.IsNullOrEmpty(email)) { TempData["ErrorMessage"] = "Không tìm thấy thông tin xác minh."; return RedirectToAction("Register"); }
        ViewBag.EmailToVerify = email;
        return View(new VerifyOtpViewModel { Email = email });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOtp(VerifyOtpViewModel model) // Dùng cho xác minh email khi đăng ký
    {
        ViewBag.EmailToVerify = model.Email;
        if (!ModelState.IsValid) { TempData.Keep("VerificationEmail"); return View(model); }

        var otpRequest = await _context.OtpRequests.Include(or => or.Customer).Where(or => or.Customer.Email == model.Email && or.OtpType.OtpTypeName == "CustomerAccountVerification" && !or.IsUsed && or.ExpirationTime > DateTime.UtcNow).OrderByDescending(or => or.RequestTime).FirstOrDefaultAsync();
        if (otpRequest == null || otpRequest.OtpCode != model.OtpCode || otpRequest.Customer == null) { ModelState.AddModelError(nameof(model.OtpCode), "Mã OTP không hợp lệ, đã hết hạn hoặc đã sử dụng."); TempData.Keep("VerificationEmail"); return View(model); }

        otpRequest.IsUsed = true; otpRequest.Status = "Verified";
        otpRequest.Customer.IsEmailVerified = true; otpRequest.Customer.EmailVerifiedAt = DateTime.UtcNow;
        try
        {
            await _context.SaveChangesAsync();
            var identity = CreateClaimsIdentity(otpRequest.Customer.Username, "Customer", otpRequest.Customer.CustomerID.ToString());
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7) });
            TempData["SuccessMessage"] = "Xác minh email thành công! Bạn đã được đăng nhập.";
            TempData.Remove("VerificationEmail");
            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex) { _logger.LogError(ex, "Lỗi lưu sau khi xác minh OTP cho {Email}.", model.Email); ModelState.AddModelError(string.Empty, "Lỗi cập nhật trạng thái xác minh."); TempData.Keep("VerificationEmail"); }
        return View(model);
    }

    // --- [HttpGet] ResendVerificationOtp: Gửi lại OTP xác minh ĐĂNG KÝ ---
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ResendVerificationOtp(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) { TempData["ErrorMessage"] = "Email không hợp lệ."; return RedirectToAction("Register"); }
        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Email == email);
        if (customer == null) { TempData["ErrorMessage"] = "Không tìm thấy tài khoản."; return RedirectToAction("Register"); }
        if (customer.IsEmailVerified) { TempData["InfoMessage"] = "Email đã được xác minh."; return RedirectToAction(nameof(Login)); }

        var otpTypeName = "CustomerAccountVerification";
        var otpType = await _context.OtpTypes.FirstOrDefaultAsync(ot => ot.OtpTypeName == otpTypeName);
        if (otpType == null) { TempData["ErrorMessage"] = "Lỗi hệ thống (OTP Type)."; TempData["VerificationEmail"] = customer.Email; return RedirectToAction(nameof(VerifyOtp)); }

        var recentOtps = await _context.OtpRequests.Where(o => o.CustomerID == customer.CustomerID && o.OtpTypeID == otpType.OtpTypeID && o.RequestTime > DateTime.UtcNow.AddMinutes(-OtpResendLockoutMinutes)).ToListAsync();
        if (recentOtps.Count >= MaxOtpResendAttempts) { TempData["ErrorMessage"] = $"Quá nhiều yêu cầu. Vui lòng thử lại sau {OtpResendLockoutMinutes} phút."; TempData["VerificationEmail"] = customer.Email; return RedirectToAction(nameof(VerifyOtp)); }

        recentOtps.Where(o => !o.IsUsed && o.ExpirationTime > DateTime.UtcNow).ToList().ForEach(o => { o.IsUsed = true; o.Status = "InvalidatedByResend"; });
        string newOtpCode = GenerateOtp();
        var otpRequest = new OtpRequest { CustomerID = customer.CustomerID, OtpTypeID = otpType.OtpTypeID, OtpCode = newOtpCode, RequestTime = DateTime.UtcNow, ExpirationTime = DateTime.UtcNow.AddMinutes(15), IsUsed = false, Status = "PendingVerification" };
        _context.OtpRequests.Add(otpRequest);
        try { await _context.SaveChangesAsync(); string emailSubject = "Mã OTP xác minh MiniMart mới"; string emailMessage = $"<p>Chào {customer.FirstName},</p><p>Mã OTP mới: <strong>{newOtpCode}</strong></p>"; await _emailSender.SendEmailAsync(customer.Email, emailSubject, emailMessage); TempData["VerificationEmail"] = customer.Email; TempData["InfoMessage"] = "OTP mới đã gửi."; }
        catch (Exception ex) { _logger.LogError(ex, "Lỗi gửi lại OTP cho {Email}.", customer.Email); TempData["VerificationEmail"] = customer.Email; TempData["ErrorMessage"] = "Lỗi gửi lại OTP."; }
        return RedirectToAction(nameof(VerifyOtp));
    }

    // --- [HttpGet] ResendLoginOtp: Gửi lại OTP cho ĐĂNG NHẬP 2FA ---
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ResendLoginOtp(string username, string userType)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(userType)) { TempData["ErrorMessage"] = "Thông tin không hợp lệ."; return RedirectToAction("Index", "Home"); }
        _logger.LogInformation("Yêu cầu gửi lại Login OTP cho {UserType} {Username}", userType, username);
        string? userEmailForOtp = null; int? customerIdForOtp = null; int? employeeAccountIdForOtp = null;

        if (userType == "Customer") { var c = await _context.Customers.FirstOrDefaultAsync(x => x.Username == username); if (c == null) { TempData["ErrorMessage"] = "Tài khoản không tồn tại."; return RedirectToAction("Index", "Home"); } userEmailForOtp = c.Email; customerIdForOtp = c.CustomerID; }
        else if (userType == "Employee") { var ea = await _context.EmployeeAccounts.Include(x => x.Employee).FirstOrDefaultAsync(x => x.Username == username); if (ea?.Employee == null) { TempData["ErrorMessage"] = "Tài khoản nhân viên không tồn tại."; return RedirectToAction("Index", "Home"); } userEmailForOtp = ea.Employee.Email; employeeAccountIdForOtp = ea.AccountID; }
        else { TempData["ErrorMessage"] = "Loại người dùng không hợp lệ."; return RedirectToAction("Index", "Home"); }

        if (string.IsNullOrEmpty(userEmailForOtp)) { TempData["ErrorMessage"] = "Không thể xác định email."; TempData["2FA_Attempt_Username"] = username; TempData["2FA_Attempt_UserType"] = userType; return RedirectToAction(nameof(VerifyLoginOtp)); }

        var otpTypeName = "LoginTwoFactorVerification";
        var otpType = await _context.OtpTypes.FirstOrDefaultAsync(ot => ot.OtpTypeName == otpTypeName);
        if (otpType == null) { _logger.LogError("CRITICAL: OtpType '{OtpTypeName}' not found for Resend Login OTP.", otpTypeName); TempData["ErrorMessage"] = "Lỗi hệ thống (OTP Type)."; TempData["2FA_Attempt_Username"] = username; TempData["2FA_Attempt_UserType"] = userType; TempData["2FA_Attempt_EmailForDisplay"] = userEmailForOtp; return RedirectToAction(nameof(VerifyLoginOtp)); }

        IQueryable<OtpRequest> recentOtpQuery = _context.OtpRequests.Where(o => o.OtpTypeID == otpType.OtpTypeID && o.RequestTime > DateTime.UtcNow.AddMinutes(-OtpResendLockoutMinutes));
        if (customerIdForOtp.HasValue) recentOtpQuery = recentOtpQuery.Where(o => o.CustomerID == customerIdForOtp.Value); else if (employeeAccountIdForOtp.HasValue) recentOtpQuery = recentOtpQuery.Where(o => o.EmployeeAccountID == employeeAccountIdForOtp.Value);

        var recentOtps = await recentOtpQuery.ToListAsync();
        if (recentOtps.Count >= MaxOtpResendAttempts) { TempData["ErrorMessage"] = $"Quá nhiều yêu cầu. Vui lòng thử lại sau {OtpResendLockoutMinutes} phút."; TempData["2FA_Attempt_Username"] = username; TempData["2FA_Attempt_UserType"] = userType; TempData["2FA_Attempt_EmailForDisplay"] = userEmailForOtp; return RedirectToAction(nameof(VerifyLoginOtp)); }

        recentOtps.Where(o => !o.IsUsed && o.ExpirationTime > DateTime.UtcNow).ToList().ForEach(o => { o.IsUsed = true; o.Status = "InvalidatedByResend"; });
        string newOtpCode = GenerateOtp();
        var newOtpRequest = new OtpRequest { CustomerID = customerIdForOtp, EmployeeAccountID = employeeAccountIdForOtp, OtpTypeID = otpType.OtpTypeID, OtpCode = newOtpCode, RequestTime = DateTime.UtcNow, ExpirationTime = DateTime.UtcNow.AddMinutes(5), IsUsed = false, Status = "PendingLogin2FA" };
        _context.OtpRequests.Add(newOtpRequest);
        try { await _context.SaveChangesAsync(); string emailSubject = "Mã Xác Thực Đăng Nhập MiniMart Mới"; string emailMessage = $"<p>Chào {username},</p><p>Mã OTP mới: <strong>{newOtpCode}</strong></p>"; await _emailSender.SendEmailAsync(userEmailForOtp, emailSubject, emailMessage); _logger.LogInformation("OTP 2FA mới đã gửi lại cho {UserType} {Username} tới {Email}.", userType, username, userEmailForOtp); TempData["InfoMessage"] = "OTP mới đã được gửi."; }
        catch (Exception ex) { _logger.LogError(ex, "Lỗi gửi lại Login OTP cho {UserType} {Username} tới {Email}.", userType, username, userEmailForOtp); TempData["ErrorMessage"] = "Lỗi gửi lại OTP."; }

        TempData["2FA_Attempt_Username"] = username; TempData["2FA_Attempt_UserType"] = userType; TempData["2FA_Attempt_EmailForDisplay"] = userEmailForOtp;
        TempData.Keep("2FA_Attempt_RememberMe"); TempData.Keep("2FA_Attempt_Role"); TempData.Keep("2FA_Attempt_UserId"); // Giữ lại các thông tin này
        return RedirectToAction(nameof(VerifyLoginOtp));
    }

    // --- Hàm tiện ích ---
    private bool VerifyPassword(string password, byte[] storedHash, byte[] storedSalt)
    {
        if (storedHash == null || storedSalt == null || storedHash.Length != 32 || storedSalt.Length != 16) { _logger.LogWarning("Xác minh mật khẩu thất bại: hash/salt không hợp lệ."); return false; }
        using var sha256 = SHA256.Create(); var combinedBytes = Encoding.UTF8.GetBytes(password).Concat(storedSalt).ToArray(); var computedHash = sha256.ComputeHash(combinedBytes); return CryptographicOperations.FixedTimeEquals(storedHash, computedHash);
    }
    private (byte[] passwordHash, byte[] salt) GeneratePasswordHashAndSalt(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16); using var sha256 = SHA256.Create(); var combinedBytes = Encoding.UTF8.GetBytes(password).Concat(salt).ToArray(); var computedHash = sha256.ComputeHash(combinedBytes); return (computedHash, salt);
    }
    private string GenerateOtp(int length = 6)
    {
        const string chars = "0123456789"; return new string(Enumerable.Range(0, length).Select(_ => chars[RandomNumberGenerator.GetInt32(chars.Length)]).ToArray());
    }
    private ClaimsIdentity CreateClaimsIdentity(string username, string role, string userId)
    {
        var claims = new List<Claim> { new Claim(ClaimTypes.Name, username), new Claim(ClaimTypes.Role, role), new Claim(ClaimTypes.NameIdentifier, userId) };
        return new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    }
    private IActionResult RedirectBasedOnRole(ClaimsPrincipal user)
    {
        if (user.IsInRole("Admin") || user.IsInRole("Staff")) { return RedirectToAction("Index", "Home"); }
        return RedirectToAction("Index", "Home");
    }
    private Dictionary<string, string[]> ModelStateToDictionary(ModelStateDictionary modelState)
    {
        return modelState.Where(x => x.Value != null && x.Value.Errors.Any()).ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray());
    }

    // --- Helper method to keep 2FA TempData ---
    private void Keep2FATempData()
    {
        TempData.Keep("2FA_Attempt_Username");
        TempData.Keep("2FA_Attempt_UserType");
        TempData.Keep("2FA_Attempt_EmailForDisplay");
        TempData.Keep("2FA_Attempt_RememberMe");
        TempData.Keep("2FA_Attempt_Role");
        TempData.Keep("2FA_Attempt_UserId");
    }

    // --- Helper method to clear 2FA TempData ---
    private void Clear2FATempData()
    {
        TempData.Remove("2FA_Attempt_Username");
        TempData.Remove("2FA_Attempt_UserType");
        TempData.Remove("2FA_Attempt_EmailForDisplay");
        TempData.Remove("2FA_Attempt_RememberMe");
        TempData.Remove("2FA_Attempt_Role");
        TempData.Remove("2FA_Attempt_UserId");
    }
    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPassword()
    {
        _logger.LogInformation("ForgotPassword (GET) action called.");
        return View(new ForgotPasswordViewModel()); // Truyền ViewModel trống cho view
    }

    // --- [HttpPost] ForgotPassword: Xử lý yêu cầu, gửi OTP ---
    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }
        _logger.LogInformation("ForgotPassword (POST) attempt for Email: {Email}, UserType: {UserType}", model.Email, model.UserType);


        string otpTypeName = string.Empty;
        Customer? customer = null; // Sử dụng nullable type
        EmployeeAccount? employeeAccount = null; // Sử dụng nullable type
        string? userEmailForOtp = null; // Email thực tế sẽ gửi OTP đến
        string? userFirstNameForEmail = null; // Tên để cá nhân hóa email


        if (model.UserType == "Customer")
        {
            customer = await _context.Customers.FirstOrDefaultAsync(c => c.Email.ToLower() == model.Email.ToLower());
            if (customer != null)
            {
                userEmailForOtp = customer.Email;
                userFirstNameForEmail = customer.FirstName;
                otpTypeName = "CustomerPasswordReset";
            }
        }
        else if (model.UserType == "Employee")
        {
            // Tìm EmployeeAccount dựa trên Email của Employee liên kết
            employeeAccount = await _context.EmployeeAccounts
                                        .Include(ea => ea.Employee)
                                        .FirstOrDefaultAsync(ea => ea.Employee.Email.ToLower() == model.Email.ToLower());
            if (employeeAccount?.Employee != null)
            {
                userEmailForOtp = employeeAccount.Employee.Email;
                userFirstNameForEmail = employeeAccount.Employee.FirstName;
                otpTypeName = "EmployeePasswordReset";
            }
        }
        else
        {
            ModelState.AddModelError(nameof(model.UserType), "Loại tài khoản không hợp lệ.");
            return View(model);
        }

        if (userEmailForOtp == null)
        {
            _logger.LogWarning("ForgotPassword: Không tìm thấy tài khoản cho Email {Email} và UserType {UserType}.", model.Email, model.UserType);
            // Không thông báo rõ email không tồn tại để bảo mật.
            TempData["InfoMessage"] = "Nếu địa chỉ email của bạn tồn tại trong hệ thống và khớp với loại tài khoản đã chọn, bạn sẽ nhận được một email hướng dẫn đặt lại mật khẩu trong vài phút. Vui lòng kiểm tra cả hộp thư Spam/Junk.";
            return RedirectToAction(nameof(ForgotPasswordConfirmation));
        }

        var otpTypeForReset = await _context.OtpTypes.FirstOrDefaultAsync(ot => ot.OtpTypeName == otpTypeName);
        if (otpTypeForReset == null)
        {
            _logger.LogError("CRITICAL: OtpType '{OtpTypeName}' not found for Password Reset.", otpTypeName);
            TempData["ErrorMessage"] = "Lỗi hệ thống, không thể xử lý yêu cầu đặt lại mật khẩu.";
            return RedirectToAction(nameof(ForgotPassword)); // Hoặc một trang lỗi chung
        }

        // Kiểm tra giới hạn gửi lại OTP
        IQueryable<OtpRequest> recentOtpQuery = _context.OtpRequests
            .Where(o => o.OtpTypeID == otpTypeForReset.OtpTypeID &&
                        o.RequestTime > DateTime.UtcNow.AddMinutes(-OtpResendLockoutMinutes));

        if (customer != null) recentOtpQuery = recentOtpQuery.Where(o => o.CustomerID == customer.CustomerID);
        else if (employeeAccount != null) recentOtpQuery = recentOtpQuery.Where(o => o.EmployeeAccountID == employeeAccount.AccountID);

        var recentOtpRequests = await recentOtpQuery.ToListAsync();
        if (recentOtpRequests.Count >= MaxOtpResendAttempts)
        {
            _logger.LogWarning("Quá nhiều yêu cầu đặt lại mật khẩu cho {Email}. Lần cuối: {LastRequestTime}", model.Email, recentOtpRequests.Max(r => r.RequestTime));
            TempData["ErrorMessage"] = $"Bạn đã yêu cầu đặt lại mật khẩu quá nhiều lần. Vui lòng thử lại sau {OtpResendLockoutMinutes} phút.";
            return RedirectToAction(nameof(ForgotPasswordConfirmation)); // Chuyển đến trang thông báo chung
        }


        // Vô hiệu hóa OTP đặt lại mật khẩu cũ (nếu có)
        var activeOldOtps = recentOtpRequests.Where(o => !o.IsUsed && o.ExpirationTime > DateTime.UtcNow).ToList();
        activeOldOtps.ForEach(o => { o.IsUsed = true; o.Status = "InvalidatedPasswordReset"; });

        string otpCode = GenerateOtp();
        var otpRequest = new OtpRequest
        {
            CustomerID = customer?.CustomerID, // Dùng customer?.CustomerID
            EmployeeAccountID = employeeAccount?.AccountID, // Dùng employeeAccount?.AccountID
            OtpTypeID = otpTypeForReset.OtpTypeID,
            OtpCode = otpCode,
            RequestTime = DateTime.UtcNow,
            ExpirationTime = DateTime.UtcNow.AddHours(1), // OTP đặt lại mật khẩu có thể có thời gian dài hơn
            IsUsed = false,
            Status = "PendingPasswordReset"
        };
        _context.OtpRequests.Add(otpRequest);
        await _context.SaveChangesAsync();

        string emailSubject = "Đặt lại mật khẩu tài khoản MiniMart của bạn";
        string emailMessage = $@"
            <html><body>
                <p>Xin chào {userFirstNameForEmail ?? model.Email.Split('@')[0]},</p>
                <p>Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản MiniMart liên kết với email này.</p>
                <p>Mã OTP để đặt lại mật khẩu của bạn là: <strong>{otpCode}</strong></p>
                <p>Mã này sẽ hết hạn sau 1 giờ.</p>
                <p>Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này.</p>
                <p>Để đặt lại mật khẩu, vui lòng truy cập trang sau và nhập mã OTP này:</p>
                <p><a href='{Url.Action("ResetPassword", "Account", new { email = model.Email, userType = model.UserType }, Request.Scheme)}'>Đặt lại mật khẩu tại đây</a></p>
            </body></html>";

        await _emailSender.SendEmailAsync(userEmailForOtp, emailSubject, emailMessage);
        _logger.LogInformation("OTP đặt lại mật khẩu đã gửi đến {Email} cho {UserType}", userEmailForOtp, model.UserType);

        TempData["InfoMessage"] = "Một email chứa mã OTP và hướng dẫn đặt lại mật khẩu đã được gửi đến địa chỉ email của bạn (nếu tồn tại và khớp với loại tài khoản). Vui lòng kiểm tra hộp thư đến và cả mục thư rác/spam.";
        // Chuyển hướng đến trang ResetPassword, truyền email và userType để tự động điền
        return RedirectToAction(nameof(ResetPassword), new { email = userEmailForOtp, userType = model.UserType });
    }

    // --- [HttpGet] ForgotPasswordConfirmation: Hiển thị thông báo sau khi yêu cầu quên mật khẩu ---
    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPasswordConfirmation()
    {
        // View này chỉ hiển thị thông báo từ TempData["InfoMessage"] hoặc ViewBag.Message
        ViewBag.Message = TempData["InfoMessage"] ?? "Nếu địa chỉ email của bạn tồn tại trong hệ thống, bạn sẽ nhận được hướng dẫn đặt lại mật khẩu.";
        if (TempData["ErrorMessage"] != null)
        {
            ViewBag.ErrorMessage = TempData["ErrorMessage"];
        }
        return View();
    }


    // --- [HttpGet] ResetPassword: Hiển thị form nhập OTP và mật khẩu mới ---
    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResetPassword(string email, string userType)
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(userType))
        {
            TempData["ErrorMessage"] = "Yêu cầu đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.";
            return RedirectToAction(nameof(ForgotPassword));
        }
        // Hiển thị thông báo từ ForgotPassword (nếu có)
        ViewBag.InfoMessageFromPrevStep = TempData["InfoMessage"] as string; // Lấy từ TempData

        var model = new ResetPasswordViewModel { Email = email, UserType = userType };
        return View(model);
    }

    // --- [HttpPost] ResetPassword: Xử lý việc đặt lại mật khẩu ---
    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        ViewBag.InfoMessageFromPrevStep = TempData["InfoMessage"] as string; // Giữ lại cho trường hợp lỗi

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        string otpTypeName = model.UserType == "Customer" ? "CustomerPasswordReset" : "EmployeePasswordReset";
        OtpRequest? otpRequest = null;
        int? targetUserId = null; // CustomerID hoặc EmployeeAccountID

        if (model.UserType == "Customer")
        {
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Email.ToLower() == model.Email.ToLower());
            if (customer != null)
            {
                targetUserId = customer.CustomerID;
                otpRequest = await _context.OtpRequests
                    .FirstOrDefaultAsync(or => or.CustomerID == customer.CustomerID &&
                                                or.OtpCode == model.OtpCode &&
                                                or.OtpType.OtpTypeName == otpTypeName &&
                                                !or.IsUsed &&
                                                or.ExpirationTime > DateTime.UtcNow);
            }
        }
        else if (model.UserType == "Employee")
        {
            var employeeAccount = await _context.EmployeeAccounts.Include(ea => ea.Employee)
                                        .FirstOrDefaultAsync(ea => ea.Employee.Email.ToLower() == model.Email.ToLower());
            if (employeeAccount != null)
            {
                targetUserId = employeeAccount.AccountID; // Lưu AccountID của EmployeeAccount
                otpRequest = await _context.OtpRequests
                    .FirstOrDefaultAsync(or => or.EmployeeAccountID == employeeAccount.AccountID &&
                                                or.OtpCode == model.OtpCode &&
                                                or.OtpType.OtpTypeName == otpTypeName &&
                                                !or.IsUsed &&
                                                or.ExpirationTime > DateTime.UtcNow);
            }
        }

        if (otpRequest == null)
        {
            ModelState.AddModelError(nameof(model.OtpCode), "Mã OTP không hợp lệ, đã hết hạn hoặc đã được sử dụng. Vui lòng thử yêu cầu lại.");
            _logger.LogWarning("Reset Password: OTP không hợp lệ cho Email {Email}, UserType {UserType}, OTP {OtpCode}", model.Email, model.UserType, model.OtpCode);
            return View(model);
        }

        (byte[] newPasswordHash, byte[] newSalt) = GeneratePasswordHashAndSalt(model.NewPassword);
        bool passwordUpdated = false;

        if (model.UserType == "Customer" && otpRequest.CustomerID.HasValue)
        {
            var customerToUpdate = await _context.Customers.FindAsync(otpRequest.CustomerID.Value);
            if (customerToUpdate != null)
            {
                customerToUpdate.PasswordHash = newPasswordHash;
                customerToUpdate.Salt = newSalt;
                passwordUpdated = true;
                _logger.LogInformation("Mật khẩu đã được đặt lại cho Khách hàng: {Username}", customerToUpdate.Username);
            }
        }
        else if (model.UserType == "Employee" && otpRequest.EmployeeAccountID.HasValue)
        {
            var employeeAccountToUpdate = await _context.EmployeeAccounts.FindAsync(otpRequest.EmployeeAccountID.Value);
            if (employeeAccountToUpdate != null)
            {
                employeeAccountToUpdate.PasswordHash = newPasswordHash;
                employeeAccountToUpdate.Salt = newSalt;
                passwordUpdated = true;
                _logger.LogInformation("Mật khẩu đã được đặt lại cho Nhân viên: {Username}", employeeAccountToUpdate.Username);
            }
        }

        if (passwordUpdated)
        {
            otpRequest.IsUsed = true;
            otpRequest.Status = "VerifiedPasswordReset";
            try
            {
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Mật khẩu của bạn đã được đặt lại thành công. Vui lòng đăng nhập bằng mật khẩu mới.";
                // Chuyển đến trang đăng nhập (Panel AJAX sẽ tự mở nếu người dùng click nút đăng nhập)
                return RedirectToAction("Index", "Home"); // Hoặc một trang thông báo thành công riêng rồi mới tới Login
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lưu thay đổi sau khi đặt lại mật khẩu cho {Email}.", model.Email);
                ModelState.AddModelError(string.Empty, "Đã xảy ra lỗi khi cập nhật mật khẩu. Vui lòng thử lại.");
            }
        }
        else
        {
            _logger.LogError("Không thể tìm thấy tài khoản để cập nhật mật khẩu cho Email {Email}, UserType {UserType} mặc dù OTP hợp lệ.", model.Email, model.UserType);
            ModelState.AddModelError(string.Empty, "Không thể cập nhật mật khẩu cho tài khoản này.");
        }
        return View(model);
    }
    [Authorize] // Yêu cầu người dùng phải đăng nhập để xem trang này
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        _logger.LogInformation("GET /Account/Profile accessed.");

        // 1. Lấy CustomerID của người dùng đang đăng nhập
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int customerId))
        {
            _logger.LogWarning("Profile GET: User not authenticated or CustomerID claim is missing/invalid.");
            // Có thể redirect đến trang đăng nhập hoặc trả về lỗi Unauthorized
            return Challenge(); // Hoặc return Unauthorized(); hoặc return RedirectToAction("Login");
        }

        _logger.LogInformation("Profile GET: Attempting to load profile for CustomerID: {CustomerId}", customerId);

        // 2. Truy vấn thông tin khách hàng từ CSDL
        var customer = await _context.Customers.FindAsync(customerId);

        if (customer == null)
        {
            _logger.LogWarning("Profile GET: Customer with ID {CustomerId} not found.", customerId);
            return NotFound($"Không tìm thấy thông tin khách hàng với ID: {customerId}.");
        }

        // 3. Map dữ liệu từ entity Customer sang CustomerProfileViewModel
        var viewModel = new CustomerProfileViewModel
        {
            CustomerId = customer.CustomerID,
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            Email = customer.Email,
            IsEmailVerified = customer.IsEmailVerified,
           // EmailVerifiedAt = customer.EmailVerifiedAt,
            PhoneNumber = customer.PhoneNumber,
            ImagePath = customer.ImagePath, // Đường dẫn ảnh sẽ được xử lý trong View
            Username = customer.Username
        };

        // --- LẤY DỮ LIỆU CHO BIỂU ĐỒ CHI TIÊU 12 THÁNG (THEO THÁNG) ---
        try
        {
            var today = DateTime.UtcNow.Date; // Hoặc DateTime.Now.Date nếu múi giờ server là giờ địa phương
                                              // Ngày đầu tiên của tháng cách đây 11 tháng (để bao gồm cả tháng hiện tại là tháng thứ 12)
            var startDateFor12Months = new DateTime(today.Year, today.Month, 1).AddMonths(-11);

            _logger.LogInformation("[ChartData] Querying sales from {StartDate} to {EndDate} for CustomerID: {CustomerId} to aggregate by month.",
                startDateFor12Months.ToString("yyyy-MM-dd"),
                today.ToString("yyyy-MM-dd"),
                customerId);

            // Lấy tất cả SaleDetails trong khoảng 12 tháng để tính tổng theo tháng
            var monthlySpendingDetails = await _context.Sales
                .Where(s => s.CustomerID == customerId &&
                            s.SaleDate >= startDateFor12Months &&
                            s.SaleDate < today.AddDays(1) && // Đến hết ngày hôm nay
                            s.OrderStatus == "Hoàn thành")
                .SelectMany(s => s.SaleDetails.Select(sd => new {
                    SaleYear = s.SaleDate.Year,
                    SaleMonth = s.SaleDate.Month,
                    Amount = sd.Quantity * sd.ProductPriceAtPurchase
                }))
                .ToListAsync();

            _logger.LogInformation("[ChartData] Fetched {Count} sale detail entries for the 12-month period.", monthlySpendingDetails.Count);

            var chartLabels = new List<string>();
            var chartData = new List<decimal>();
            decimal totalSpendingThisYear = 0; // Biến để tính tổng chi tiêu của năm hiện tại (tùy chọn)
            int currentYearForTotal = today.Year; // Năm hiện tại để tính tổng

            if (monthlySpendingDetails.Any())
            {
                // Nhóm dữ liệu đã lấy theo Tháng và Năm, sau đó tính tổng
                var spendingGroupedByMonth = monthlySpendingDetails
                    .GroupBy(x => new { x.SaleYear, x.SaleMonth })
                    .Select(g => new
                    {
                        MonthYearKey = new DateTime(g.Key.SaleYear, g.Key.SaleMonth, 1),
                        TotalAmount = g.Sum(x => x.Amount)
                    })
                    .OrderBy(x => x.MonthYearKey)
                    .ToList();

                // Điền dữ liệu cho 12 tháng trên biểu đồ
                for (int i = 0; i < 12; i++)
                {
                    // Bắt đầu từ 11 tháng trước, tiến tới tháng hiện tại
                    var targetMonthDate = startDateFor12Months.AddMonths(i);
                    chartLabels.Add($"T{targetMonthDate.Month}/{targetMonthDate.ToString("yy")}");

                    var spendingForThisMonth = spendingGroupedByMonth.FirstOrDefault(sbm => sbm.MonthYearKey.Year == targetMonthDate.Year && sbm.MonthYearKey.Month == targetMonthDate.Month);
                    var monthlyTotal = spendingForThisMonth?.TotalAmount ?? 0m;
                    chartData.Add(monthlyTotal);

                    // Nếu bạn muốn tính tổng chi tiêu cho năm hiện tại (ví dụ: 2025)
                    if (targetMonthDate.Year == currentYearForTotal)
                    {
                        totalSpendingThisYear += monthlyTotal;
                    }
                }
                ViewBag.ChartTitle = $"Chi tiêu 12 tháng qua";
            }
            else
            {
                // Nếu không có dữ liệu, tạo nhãn trống và dữ liệu 0 cho 12 tháng
                for (int i = 0; i < 12; i++)
                {
                    var monthToDisplay = startDateFor12Months.AddMonths(i);
                    chartLabels.Add($"T{monthToDisplay.Month}/{monthToDisplay.ToString("yy")}");
                    chartData.Add(0m);
                }
                ViewBag.ChartTitle = "Chưa có dữ liệu chi tiêu trong 12 tháng qua";
            }

            ViewBag.ChartLabels = chartLabels;
            ViewBag.ChartData = chartData;
            ViewBag.TotalSpendingThisYear = totalSpendingThisYear; // Truyền tổng chi tiêu năm nay qua ViewBag (tùy chọn)

            _logger.LogInformation("[ChartData] Prepared 12-Month Chart Labels ({Count}): {Labels}", chartLabels.Count, string.Join(", ", chartLabels));
            _logger.LogInformation("[ChartData] Prepared 12-Month Chart Data ({Count}): {DataValues}", chartData.Count, string.Join(", ", chartData.Select(d => d.ToString("N0"))));
            if (totalSpendingThisYear > 0)
            {
                _logger.LogInformation("[ChartData] Total spending for year {Year}: {TotalAmount}", currentYearForTotal, totalSpendingThisYear.ToString("N0"));
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ChartData] Error fetching 12-month spending data for CustomerID: {CustomerId}", customerId);
            ViewBag.ChartLabels = Enumerable.Range(0, 12).Select(i => DateTime.UtcNow.AddMonths(i - 11).ToString("TMM/yy")).ToList();
            ViewBag.ChartData = Enumerable.Repeat(0m, 12).ToList();
            ViewBag.ChartTitle = "Lỗi tải dữ liệu chi tiêu";
            ViewBag.TotalSpendingThisYear = 0m;
        }
        // --- KẾT THÚC LẤY DỮ LIỆU BIỂU ĐỒ ---

        _logger.LogInformation("Profile GET: Profile loaded successfully for CustomerID: {CustomerId}, Username: {Username}", customerId, customer.Username);
        return View(viewModel); // Trả về View "Profile.cshtml" với dữ liệu viewModel
    }

    // POST: /Account/Profile
    [Authorize] // Yêu cầu người dùng phải đăng nhập
    [HttpPost]
    [ValidateAntiForgeryToken] // Chống tấn công CSRF
    public async Task<IActionResult> Profile(CustomerProfileViewModel model)
    {
        _logger.LogInformation("POST /Account/Profile received for CustomerID from model: {ModelCustomerId}", model.CustomerId);

        // 1. Xác thực người dùng và CustomerId
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int currentUserId) || currentUserId != model.CustomerId)
        {
            _logger.LogWarning("Profile POST: Unauthorized attempt. Logged in UserID: {CurrentUserId}, Model CustomerID: {ModelCustomerId}", userIdString, model.CustomerId);
            return Forbid(); // Hoặc Unauthorized() - người dùng không có quyền sửa profile này
        }

        // 2. Kiểm tra ModelState
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Profile POST: ModelState is invalid for CustomerID: {CustomerId}", model.CustomerId);
            // Nếu có lỗi validation, cần load lại ImagePath vì nó không được post lại từ form
            // Chúng ta có thể lấy từ TempData hoặc truy vấn lại nhanh nếu không muốn lộ liễu quá
            // Hoặc đơn giản là ViewModel đã có ImagePath (nếu bạn thêm hidden input cho nó)
            // Nếu bạn đã có <input type="hidden" asp-for="ImagePath" /> trong form thì không cần làm gì thêm ở đây cho ImagePath
            return View(model); // Trả về view với các lỗi validation
        }

        // 3. Lấy entity Customer từ DB để cập nhật
        var customerToUpdate = await _context.Customers.FindAsync(model.CustomerId);
        if (customerToUpdate == null)
        {
            _logger.LogError("Profile POST: Customer with ID {CustomerId} not found for update.", model.CustomerId);
            return NotFound($"Không tìm thấy khách hàng để cập nhật.");
        }

        // 4. Cập nhật các thuộc tính được phép thay đổi
        customerToUpdate.FirstName = model.FirstName;
        customerToUpdate.LastName = model.LastName;
        customerToUpdate.PhoneNumber = model.PhoneNumber;
        // Lưu ý: Email, Username, IsEmailVerified không nên được cập nhật trực tiếp từ form này.
        // Việc thay đổi email cần có quy trình xác minh riêng.

        // 5. Xử lý upload ảnh đại diện mới (nếu có)
        if (model.NewImageFile != null && model.NewImageFile.Length > 0)
        {
            _logger.LogInformation("Profile POST: New image file detected for CustomerID: {CustomerId}. File name: {FileName}, Size: {FileSize}", model.CustomerId, model.NewImageFile.FileName, model.NewImageFile.Length);
            try
            {
                // Đường dẫn đến thư mục lưu ảnh (ví dụ: wwwroot/images/users)
                // Đảm bảo _webHostEnvironment đã được inject vào constructor
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "users");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                    _logger.LogInformation("Profile POST: Created directory: {UploadsFolder}", uploadsFolder);
                }

                // Xóa ảnh cũ (nếu có và không phải là ảnh mặc định)
                if (!string.IsNullOrEmpty(customerToUpdate.ImagePath) && customerToUpdate.ImagePath != "default.jpg") // Thay "default.jpg" bằng tên ảnh mặc định của bạn
                {
                    var oldImagePath = Path.Combine(uploadsFolder, customerToUpdate.ImagePath);
                    if (System.IO.File.Exists(oldImagePath))
                    {
                        System.IO.File.Delete(oldImagePath);
                        _logger.LogInformation("Profile POST: Deleted old image: {OldImagePath}", oldImagePath);
                    }
                }

                // Tạo tên file duy nhất để tránh trùng lặp
                string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(model.NewImageFile.FileName);
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                // Lưu file mới
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await model.NewImageFile.CopyToAsync(fileStream);
                }
                customerToUpdate.ImagePath = uniqueFileName; // Lưu tên file mới vào DB
                _logger.LogInformation("Profile POST: New image saved as: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Profile POST: Error uploading new image for CustomerID: {CustomerId}", model.CustomerId);
                ModelState.AddModelError("NewImageFile", "Lỗi tải lên ảnh đại diện. Vui lòng thử lại.");
                // Có thể cần load lại ImagePath hiện tại của model nếu return View(model)
                model.ImagePath = customerToUpdate.ImagePath; // Giữ lại ảnh cũ nếu upload lỗi
                return View(model);
            }
        }

        // 6. Lưu thay đổi vào CSDL
        try
        {
            _context.Update(customerToUpdate);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Profile POST: Profile updated successfully for CustomerID: {CustomerId}, Username: {Username}", customerToUpdate.CustomerID, customerToUpdate.Username);

            TempData["SuccessMessage"] = "Thông tin cá nhân đã được cập nhật thành công!";
            return RedirectToAction(nameof(Profile)); // Chuyển hướng về trang Profile (GET) để hiển thị thông tin mới
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "Profile POST: Concurrency error updating profile for CustomerID: {CustomerId}", model.CustomerId);
            ModelState.AddModelError(string.Empty, "Lỗi lưu dữ liệu. Dữ liệu có thể đã được thay đổi bởi người khác. Vui lòng tải lại trang và thử lại.");
            // Load lại ImagePath hiện tại
            model.ImagePath = customerToUpdate.ImagePath;
            return View(model);
        }
        catch (DbUpdateException ex) // Bắt lỗi cụ thể từ DB, ví dụ UNIQUE constraint
        {
            _logger.LogError(ex, "Profile POST: Database update error for CustomerID: {CustomerId}", model.CustomerId);
            if (ex.InnerException != null && ex.InnerException.Message.Contains("UNIQUE KEY constraint", StringComparison.OrdinalIgnoreCase))
            {
                if (ex.InnerException.Message.Contains("PhoneNumber", StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(nameof(model.PhoneNumber), "Số điện thoại này đã được sử dụng bởi một tài khoản khác.");
                }
                // Thêm kiểm tra cho các cột UNIQUE khác nếu cần
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Đã xảy ra lỗi khi cập nhật thông tin. Vui lòng thử lại.");
            }
            // Load lại ImagePath hiện tại
            model.ImagePath = customerToUpdate.ImagePath;
            return View(model);
        }
    }



    // Class DTO nội bộ nhỏ (nếu bạn muốn dùng)
    private class ApplicationUser
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? FirstName { get; set; }
    }
}