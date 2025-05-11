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

    // --- [HttpPost] Login: Bước 1 - Xác thực Username/Password & Gửi OTP 2FA ---
    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { errors = ModelStateToDictionary(ModelState) });
        }

        _logger.LogInformation("Login POST: Bắt đầu xử lý đăng nhập cho UserType: {UserType}, Username: {Username}", model.UserType, model.Username);

        string? userEmailForOtp = null;
        int? customerIdForOtpRecord = null;
        int? employeeAccountIdForOtpRecord = null;
        string usernameForClaims = model.Username;
        string roleForClaims = string.Empty;
        string userIdForClaims = string.Empty;

        try
        {
            if (model.UserType == "Customer")
            {
                var customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Username == model.Username);
                if (customer == null || !VerifyPassword(model.Password, customer.PasswordHash, customer.Salt))
                {
                    _logger.LogWarning("Đăng nhập thất bại (Bước 1 - Khách hàng): Sai thông tin cho {Username}", model.Username);
                    return BadRequest(new { message = "Tên đăng nhập hoặc mật khẩu không đúng." });
                }
                if (!customer.IsEmailVerified)
                {
                    _logger.LogWarning("Đăng nhập (Bước 1 - Khách hàng): Email chưa xác minh cho {Username}", model.Username);
                    TempData["UnverifiedEmail"] = customer.Email;
                    return BadRequest(new { message = "Tài khoản của bạn chưa được xác minh email ban đầu. Vui lòng kiểm tra email hoặc yêu cầu gửi lại mã xác minh.", needsInitialVerification = true, email = customer.Email });
                }
                userEmailForOtp = customer.Email;
                customerIdForOtpRecord = customer.CustomerID;
                roleForClaims = "Customer";
                userIdForClaims = customer.CustomerID.ToString();
            }
            else if (model.UserType == "Employee")
            {
                var employeeAccount = await _context.EmployeeAccounts.AsNoTracking()
                                            .Include(ea => ea.Employee).ThenInclude(e => e.Role)
                                            .FirstOrDefaultAsync(ea => ea.Username == model.Username);
                if (employeeAccount?.Employee?.Role == null || !VerifyPassword(model.Password, employeeAccount.PasswordHash, employeeAccount.Salt))
                {
                    _logger.LogWarning("Đăng nhập thất bại (Bước 1 - Nhân viên): Sai thông tin cho {Username}", model.Username);
                    return BadRequest(new { message = "Tên đăng nhập hoặc mật khẩu không đúng." });
                }
                userEmailForOtp = employeeAccount.Employee.Email;
                employeeAccountIdForOtpRecord = employeeAccount.AccountID;
                roleForClaims = employeeAccount.Employee.Role.RoleName;
                userIdForClaims = employeeAccount.EmployeeID.ToString();
            }
            else
            {
                return BadRequest(new { message = "Loại người dùng không hợp lệ." });
            }

            if (string.IsNullOrEmpty(userEmailForOtp))
            {
                _logger.LogError("Không tìm thấy email để gửi OTP 2FA cho {Username} ({UserType})", model.Username, model.UserType);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi hệ thống: Không thể xác định email." });
            }

            string otpCode = GenerateOtp();
            var otpType = await _context.OtpTypes.FirstOrDefaultAsync(ot => ot.OtpTypeName == "LoginTwoFactorVerification");
            if (otpType == null) { _logger.LogError("CRITICAL: OtpType 'LoginTwoFactorVerification' not found."); return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi cấu hình hệ thống (OTP Type)." }); }

            var otpRequest = new OtpRequest
            {
                CustomerID = customerIdForOtpRecord,
                EmployeeAccountID = employeeAccountIdForOtpRecord,
                OtpTypeID = otpType.OtpTypeID,
                OtpCode = otpCode,
                RequestTime = DateTime.UtcNow,
                ExpirationTime = DateTime.UtcNow.AddMinutes(5),
                IsUsed = false,
                Status = "PendingLogin2FA"
            };
            _context.OtpRequests.Add(otpRequest);
            await _context.SaveChangesAsync();

            string emailSubject = "Mã Xác Thực Đăng Nhập MiniMart";
            string emailMessage = $"<p>Xin chào {model.Username},</p><p>Mã xác thực đăng nhập của bạn là: <strong>{otpCode}</strong>. Mã này sẽ hết hạn sau 5 phút.</p>";
            await _emailSender.SendEmailAsync(userEmailForOtp, emailSubject, emailMessage);

            _logger.LogInformation("Bước 1 Đăng nhập: OTP 2FA đã gửi đến {Email} cho {Username} ({UserType}).", userEmailForOtp, model.Username, model.UserType);

            TempData["2FA_Attempt_Username"] = model.Username;
            TempData["2FA_Attempt_UserType"] = model.UserType;
            TempData["2FA_Attempt_EmailForDisplay"] = userEmailForOtp;
            TempData["2FA_Attempt_RememberMe"] = model.RememberMe;
            TempData["2FA_Attempt_Role"] = roleForClaims;
            TempData["2FA_Attempt_UserId"] = userIdForClaims;

            return RedirectToAction(nameof(VerifyLoginOtp));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi không mong muốn trong Bước 1 Đăng nhập cho {Username}.", model.Username);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi hệ thống. Vui lòng thử lại." });
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
    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyLoginOtp(VerifyLoginOtpViewModel model)
    {
        var originalUsername = TempData["2FA_Attempt_Username"] as string;
        var originalUserType = TempData["2FA_Attempt_UserType"] as string;
        var emailForDisplayFromTempData = TempData["2FA_Attempt_EmailForDisplay"] as string;
        bool rememberMe = TempData["2FA_Attempt_RememberMe"] as bool? ?? false;
        string? roleForClaims = TempData["2FA_Attempt_Role"] as string;
        string? userIdForClaims = TempData["2FA_Attempt_UserId"] as string;

        ViewBag.VerifyingEmail = model.EmailForDisplay ?? emailForDisplayFromTempData;
        Keep2FATempData(); // Giữ lại TempData nếu có lỗi và return View(model)

        if (!ModelState.IsValid || model.Username != originalUsername || model.UserType != originalUserType)
        {
            _logger.LogWarning("VerifyLoginOtp (POST): ModelState không hợp lệ hoặc thông tin không khớp TempData. ModelUser: {MU}, TempUser: {TU}", model.Username, originalUsername);
            if (model.Username != originalUsername || model.UserType != originalUserType)
                ModelState.AddModelError(string.Empty, "Thông tin xác thực không khớp.");
            if (string.IsNullOrEmpty(model.Username) && !string.IsNullOrEmpty(originalUsername)) model.Username = originalUsername;
            if (string.IsNullOrEmpty(model.UserType) && !string.IsNullOrEmpty(originalUserType)) model.UserType = originalUserType;
            if (string.IsNullOrEmpty(model.EmailForDisplay) && !string.IsNullOrEmpty(emailForDisplayFromTempData)) model.EmailForDisplay = emailForDisplayFromTempData;
            return View(model);
        }

        OtpRequest? otpRequest = null;
        if (model.UserType == "Customer" && int.TryParse(userIdForClaims, out int customerId))
        {
            otpRequest = await _context.OtpRequests.FirstOrDefaultAsync(or => or.CustomerID == customerId && or.OtpCode == model.OtpCode && or.OtpType.OtpTypeName == "LoginTwoFactorVerification" && !or.IsUsed && or.ExpirationTime > DateTime.UtcNow);
        }
        else if (model.UserType == "Employee" && !string.IsNullOrEmpty(userIdForClaims))
        {
            var empAccount = await _context.EmployeeAccounts.FirstOrDefaultAsync(ea => ea.EmployeeID.ToString() == userIdForClaims);
            if (empAccount != null)
            {
                otpRequest = await _context.OtpRequests.FirstOrDefaultAsync(or => or.EmployeeAccountID == empAccount.AccountID && or.OtpCode == model.OtpCode && or.OtpType.OtpTypeName == "LoginTwoFactorVerification" && !or.IsUsed && or.ExpirationTime > DateTime.UtcNow);
            }
        }

        if (otpRequest == null)
        {
            ModelState.AddModelError(nameof(model.OtpCode), "Mã OTP không hợp lệ, đã hết hạn hoặc đã được sử dụng.");
            _logger.LogWarning("Login 2FA OTP verification failed for {UserType} {Username} with OTP {OtpCode}.", model.UserType, model.Username, model.OtpCode);
            return View(model);
        }

        otpRequest.IsUsed = true;
        otpRequest.Status = "VerifiedLogin2FA";
        try
        {
            await _context.SaveChangesAsync();
            if (string.IsNullOrEmpty(roleForClaims) || string.IsNullOrEmpty(userIdForClaims))
            {
                ModelState.AddModelError(string.Empty, "Lỗi hệ thống: Không thể hoàn tất đăng nhập (claims missing).");
                return View(model);
            }
            var identity = CreateClaimsIdentity(model.Username, roleForClaims, userIdForClaims);
            var principal = new ClaimsPrincipal(identity);
            var authProperties = new AuthenticationProperties { IsPersistent = rememberMe, ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(7) : (DateTimeOffset?)null };
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);
            _logger.LogInformation("{UserType} {Username} (Role: {Role}) đăng nhập thành công qua 2FA. RememberMe: {Remember}", model.UserType, model.Username, roleForClaims, rememberMe);
            TempData["SuccessMessage"] = "Đăng nhập thành công!";
            Clear2FATempData(); // Xóa TempData sau khi thành công
            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi lưu sau khi xác minh Login 2FA OTP cho {UserType} {Username}.", model.UserType, model.Username);
            ModelState.AddModelError(string.Empty, "Lỗi hệ thống khi hoàn tất đăng nhập.");
            return View(model);
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
        if (user.IsInRole("Admin") || user.IsInRole("Staff")) { return RedirectToAction("Index", "Dashboard", new { area = "Admin" }); }
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


    // Class DTO nội bộ nhỏ (nếu bạn muốn dùng)
    private class ApplicationUser
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? FirstName { get; set; }
    }
}