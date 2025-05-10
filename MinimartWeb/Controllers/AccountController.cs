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
using MinimartWeb.Models; // Cho LoginViewModel, RegisterViewModel, VerifyOtpViewModel, ErrorViewModel
using MinimartWeb.Model;  // Cho Customer, EmployeeAccount, Employee, EmployeeRole, OtpRequest, OtpType
using System;
using System.Collections.Generic;
using System.IO; // Cho Path, FileStream
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
    private readonly IWebHostEnvironment _webHostEnvironment; // Để xử lý ảnh

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

    // --- [HttpGet] Login: Xử lý khi người dùng truy cập trực tiếp /Account/Login ---
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectBasedOnRole(User);
        }
        // Chuyển về trang chủ nếu cố tình vào /Account/Login khi chưa đăng nhập,
        // khuyến khích dùng panel AJAX.
        return RedirectToAction("Index", "Home");
    }

    // --- [HttpPost] Login: Xử lý đăng nhập từ panel AJAX, luôn trả về JSON ---
    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            // Trả về lỗi validation dưới dạng JSON để JavaScript hiển thị
            return BadRequest(new { errors = ModelStateToDictionary(ModelState) });
        }

        ClaimsIdentity? identity = null;
        string role = string.Empty;
        string redirectUrl = Url.Action("Index", "Home") ?? "/";

        try
        {
            if (model.UserType == "Customer")
            {
                var customer = await _context.Customers.AsNoTracking()
                                       .FirstOrDefaultAsync(c => c.Username == model.Username);

                if (customer == null || !VerifyPassword(model.Password, customer.PasswordHash, customer.Salt))
                {
                    _logger.LogWarning("Đăng nhập thất bại cho khách hàng username: {Username}", model.Username);
                    return BadRequest(new { message = "Tên đăng nhập hoặc mật khẩu không đúng." });
                }

                // KIỂM TRA XÁC MINH EMAIL CHO CUSTOMER
                if (!customer.IsEmailVerified)
                {
                    _logger.LogWarning("Đăng nhập cho email khách hàng chưa xác minh: {Username}", model.Username);
                    TempData["UnverifiedEmail"] = customer.Email; // Để action ResendVerificationOtp có thể sử dụng
                    return BadRequest(new
                    {
                        message = "Tài khoản của bạn chưa được xác minh email. Vui lòng kiểm tra email hoặc yêu cầu gửi lại mã xác minh.",
                        needsVerification = true,
                        email = customer.Email // Trả về email để JS có thể dùng cho link "Gửi lại OTP"
                    });
                }

                role = "Customer";
                identity = CreateClaimsIdentity(customer.Username, role, customer.CustomerID.ToString());
            }
            else if (model.UserType == "Employee")
            {
                var employeeAccount = await _context.EmployeeAccounts.AsNoTracking()
                                            .Include(ea => ea.Employee)
                                                .ThenInclude(e => e.Role)
                                            .FirstOrDefaultAsync(ea => ea.Username == model.Username);

                if (employeeAccount?.Employee?.Role == null || !VerifyPassword(model.Password, employeeAccount.PasswordHash, employeeAccount.Salt))
                {
                    _logger.LogWarning("Đăng nhập thất bại cho nhân viên username: {Username}", model.Username);
                    return BadRequest(new { message = "Tên đăng nhập hoặc mật khẩu không đúng." });
                }

                // (Tùy chọn) Kiểm tra IsEmailVerified cho EmployeeAccount nếu bạn triển khai
                // if (employeeAccount.IsEmailVerified == false && employeeAccount.Employee.Role.RoleName != "Admin") // Ví dụ: Admin không cần xác minh email
                // {
                //     _logger.LogWarning("Đăng nhập cho email nhân viên chưa xác minh: {Username}", model.Username);
                //     TempData["UnverifiedEmail"] = employeeAccount.Employee.Email;
                //     return BadRequest(new { message = "Tài khoản nhân viên chưa xác minh email.", needsVerification = true, email = employeeAccount.Employee.Email, userType = "Employee" });
                // }

                // Logic 2FA bắt buộc cho nhân viên (trừ Admin nếu muốn)
                if (employeeAccount.Employee.Role.RoleName != "Admin") // Ví dụ: Admin không cần 2FA
                {
                    string otpCode = GenerateOtp();
                    var otpType = await _context.OtpTypes.FirstOrDefaultAsync(ot => ot.OtpTypeName == "EmployeeLoginTwoFactor");
                    if (otpType == null)
                    {
                        _logger.LogError("CRITICAL: OtpType 'EmployeeLoginTwoFactor' not found.");
                        return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi cấu hình hệ thống (2FA)." });
                    }

                    var otpRequest = new OtpRequest
                    {
                        EmployeeAccountID = employeeAccount.AccountID,
                        OtpTypeID = otpType.OtpTypeID,
                        OtpCode = otpCode,
                        RequestTime = DateTime.UtcNow,
                        ExpirationTime = DateTime.UtcNow.AddMinutes(5), // 2FA OTP thường có thời gian ngắn
                        IsUsed = false,
                        Status = "Pending2FA"
                    };
                    _context.OtpRequests.Add(otpRequest);
                    await _context.SaveChangesAsync();

                    string emailSubject2FA = "Mã xác thực hai yếu tố MiniMart";
                    string emailMessage2FA = $"<p>Mã xác thực đăng nhập của bạn là: <strong>{otpCode}</strong></p><p>Mã này sẽ hết hạn sau 5 phút.</p>";
                    await _emailSender.SendEmailAsync(employeeAccount.Employee.Email, emailSubject2FA, emailMessage2FA);

                    _logger.LogInformation("2FA OTP sent to employee {Username}", employeeAccount.Username);
                    return Ok(new { success = false, requiresTwoFactor = true, userType = "Employee", username = employeeAccount.Username, message = "Vui lòng nhập mã xác thực hai yếu tố đã gửi đến email của bạn." });
                }


                role = employeeAccount.Employee.Role.RoleName;
                identity = CreateClaimsIdentity(employeeAccount.Username, role, employeeAccount.Employee.EmployeeID.ToString());
            }
            else
            {
                _logger.LogWarning("Loại người dùng không hợp lệ khi đăng nhập: {UserType}", model.UserType);
                return BadRequest(new { message = "Loại người dùng không hợp lệ." });
            }

            var principal = new ClaimsPrincipal(identity);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(7) : null
            };
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

            _logger.LogInformation("Người dùng {Username} (Vai trò: {Role}) đăng nhập thành công. Chuyển hướng đến {RedirectUrl}", model.Username, role, redirectUrl);
            return Ok(new { success = true, redirectUrl = redirectUrl });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi không mong muốn khi đăng nhập cho người dùng {Username}.", model.Username);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Đã có lỗi hệ thống xảy ra. Vui lòng thử lại sau." });
        }
    }

    // --- [HttpPost] Logout ---
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        string? userName = User.Identity?.Name;
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        _logger.LogInformation("Người dùng {UserName} đã đăng xuất thành công.", userName ?? "Không xác định");
        // Trả về JSON với redirectUrl để JavaScript có thể xử lý chuyển hướng nếu cần
        return Ok(new { message = "Đăng xuất thành công.", redirectUrl = Url.Action("Index", "Home") });
    }

    // --- AccessDenied ---
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        _logger.LogWarning("Truy cập bị từ chối cho người dùng: {UserName}", User.Identity?.Name ?? "Ẩn danh");
        return View();
    }


    // === BẮT ĐẦU CÁC ACTION CHO ĐĂNG KÝ ===

    // --- [HttpGet] Register: Hiển thị form đăng ký ---
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectBasedOnRole(User);
        }
        return View(new RegisterViewModel());
    }

    // --- [HttpPost] Register: Xử lý việc đăng ký ---
    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        if (await _context.Customers.AnyAsync(c => c.Username.ToLower() == model.Username.ToLower()))
        {
            ModelState.AddModelError(nameof(model.Username), "Tên đăng nhập này đã được sử dụng.");
        }
        if (await _context.Customers.AnyAsync(c => c.Email.ToLower() == model.Email.ToLower()))
        {
            ModelState.AddModelError(nameof(model.Email), "Địa chỉ email này đã được sử dụng.");
        }
        if (await _context.Customers.AnyAsync(c => c.PhoneNumber == model.PhoneNumber))
        {
            ModelState.AddModelError(nameof(model.PhoneNumber), "Số điện thoại này đã được sử dụng.");
        }

        if (ModelState.IsValid)
        {
            var customer = new Customer
            {
                FirstName = model.FirstName,
                LastName = model.LastName,
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                Username = model.Username,
                IsEmailVerified = false,
                ImagePath = "default.jpg"
            };

            if (model.ProfileImage != null && model.ProfileImage.Length > 0)
            {
                try
                {
                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "users");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                    var uniqueFileName = $"{Guid.NewGuid()}{Path.GetExtension(model.ProfileImage.FileName)}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.ProfileImage.CopyToAsync(fileStream);
                    }
                    customer.ImagePath = uniqueFileName;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi tải lên ảnh đại diện cho người dùng mới {Username}.", model.Username);
                }
            }

            (byte[] passwordHash, byte[] salt) = GeneratePasswordHashAndSalt(model.Password);
            customer.PasswordHash = passwordHash;
            customer.Salt = salt;

            string otpCode = GenerateOtp();
            var otpType = await _context.OtpTypes.FirstOrDefaultAsync(ot => ot.OtpTypeName == "CustomerAccountVerification");

            if (otpType == null)
            {
                _logger.LogError("CRITICAL: OtpType 'CustomerAccountVerification' not found in database. Cannot proceed with user registration OTP.");
                ModelState.AddModelError(string.Empty, "Lỗi hệ thống: Không thể xử lý yêu cầu xác minh tài khoản. Vui lòng thử lại sau hoặc liên hệ quản trị viên.");
                return View(model);
            }

            var otpRequest = new OtpRequest
            {
                OtpTypeID = otpType.OtpTypeID,
                OtpCode = otpCode,
                RequestTime = DateTime.UtcNow,
                ExpirationTime = DateTime.UtcNow.AddMinutes(15),
                IsUsed = false,
                Status = "PendingVerification"
            };

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    _context.Customers.Add(customer);
                    await _context.SaveChangesAsync();

                    otpRequest.CustomerID = customer.CustomerID;
                    _context.OtpRequests.Add(otpRequest);
                    await _context.SaveChangesAsync();

                    string emailSubject = "Xác minh tài khoản MiniMart của bạn";
                    string emailMessage = $@"
                        <html><body>
                            <p>Chào {customer.FirstName},</p>
                            <p>Cảm ơn bạn đã đăng ký tài khoản tại MiniMart. Để hoàn tất quá trình đăng ký, vui lòng sử dụng mã OTP sau để xác minh địa chỉ email của bạn:</p>
                            <h2 style='text-align:center; color: #007bff; font-size: 28px; letter-spacing: 2px;'>{otpCode}</h2>
                            <p>Mã OTP này sẽ có hiệu lực trong vòng 15 phút.</p>
                            <p>Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email này.</p>
                            <p>Trân trọng,<br/>Đội ngũ MiniMart</p>
                        </body></html>";

                    await _emailSender.SendEmailAsync(customer.Email, emailSubject, emailMessage);
                    _logger.LogInformation("OTP email sent to {Email} for new customer {Username}.", customer.Email, customer.Username);

                    await transaction.CommitAsync();
                    _logger.LogInformation("Khách hàng mới {Username} (ID: {CustomerId}) đã đăng ký. OTP đã được gửi để xác minh.", customer.Username, customer.CustomerID);

                    TempData["VerificationEmail"] = customer.Email;
                    TempData["InfoMessage"] = "Đăng ký gần thành công! Một mã OTP đã được gửi đến email của bạn. Vui lòng kiểm tra và nhập mã để hoàn tất xác minh.";
                    return RedirectToAction("VerifyOtp", "Account");
                }
                catch (DbUpdateException dbEx)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(dbEx, "Lỗi cơ sở dữ liệu trong quá trình đăng ký cho {Username}. Chi tiết: {InnerException}", model.Username, dbEx.InnerException?.Message);
                    ModelState.AddModelError(string.Empty, "Đã xảy ra lỗi khi lưu thông tin đăng ký. Vui lòng thử lại.");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Lỗi không mong muốn trong quá trình đăng ký cho {Username}.", model.Username);
                    ModelState.AddModelError(string.Empty, "Đã có lỗi không mong muốn xảy ra. Vui lòng thử lại sau.");
                }
            }
        }

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Biểu mẫu đăng ký không hợp lệ cho người dùng thử với tên đăng nhập: {Username}", model.Username ?? "N/A");
            // Không cần log lỗi ModelState ở đây nữa vì đã log ở trên nếu có
        }
        return View(model);
    }


    // --- [HttpGet] VerifyOtp: Hiển thị form nhập OTP ---
    [AllowAnonymous]
    [HttpGet]
    public IActionResult VerifyOtp()
    {
        var email = TempData["VerificationEmail"] as string;
        if (email != null) TempData.Keep("VerificationEmail");

        if (string.IsNullOrEmpty(email))
        {
            TempData["ErrorMessage"] = "Không tìm thấy thông tin để xác minh. Vui lòng thử đăng ký lại.";
            return RedirectToAction("Register");
        }
        ViewBag.EmailToVerify = email;
        return View(new VerifyOtpViewModel { Email = email });
    }

    // --- [HttpPost] VerifyOtp: Xử lý mã OTP ---
    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOtp(VerifyOtpViewModel model)
    {
        ViewBag.EmailToVerify = model.Email; // Giữ lại email cho view nếu có lỗi

        if (!ModelState.IsValid) // ModelState cho VerifyOtpViewModel
        {
            return View(model);
        }

        var otpRequest = await _context.OtpRequests
            .Include(or => or.Customer)
            .Where(or => or.Customer.Email == model.Email &&
                          or.OtpType.OtpTypeName == "CustomerAccountVerification" && // Đúng loại OTP
                          !or.IsUsed && // Chưa được sử dụng
                          or.ExpirationTime > DateTime.UtcNow) // Còn hạn
            .OrderByDescending(or => or.RequestTime) // Lấy OTP mới nhất nếu có nhiều
            .FirstOrDefaultAsync();

        if (otpRequest == null || otpRequest.OtpCode != model.OtpCode || otpRequest.Customer == null)
        {
            ModelState.AddModelError(nameof(model.OtpCode), "Mã OTP không hợp lệ, đã hết hạn hoặc đã được sử dụng.");
            _logger.LogWarning("Xác minh OTP thất bại cho email {Email} với OTP {OtpCode}.", model.Email, model.OtpCode);
            return View(model);
        }

        otpRequest.IsUsed = true;
        otpRequest.Status = "Verified";
        otpRequest.Customer.IsEmailVerified = true;
        otpRequest.Customer.EmailVerifiedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Email đã được xác minh thành công cho khách hàng: {Email}", model.Email);

            // Tự động đăng nhập người dùng sau khi xác minh thành công
            var identity = CreateClaimsIdentity(otpRequest.Customer.Username, "Customer", otpRequest.Customer.CustomerID.ToString());
            var principal = new ClaimsPrincipal(identity);
            // IsPersistent có thể lấy từ một giá trị tạm thời nếu muốn "ghi nhớ" từ form đăng ký
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7) });
            _logger.LogInformation("Khách hàng {Username} đã tự động đăng nhập sau khi xác minh email.", otpRequest.Customer.Username);

            TempData["SuccessMessage"] = "Xác minh email thành công! Bạn đã được đăng nhập.";
            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi lưu thay đổi sau khi xác minh OTP cho email {Email}.", model.Email);
            ModelState.AddModelError(string.Empty, "Đã xảy ra lỗi khi cập nhật trạng thái xác minh. Vui lòng thử lại.");
            return View(model);
        }
    }


    // --- [HttpGet] ResendVerificationOtp: Gửi lại OTP xác minh ---
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ResendVerificationOtp(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["ErrorMessage"] = "Địa chỉ email không hợp lệ để gửi lại OTP.";
            // Nếu người dùng cố tình vào đây mà không có email, có thể chuyển về trang đăng nhập/đăng ký
            return RedirectToAction(TempData["LastValidPage"] as string ?? "Register");
        }

        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Email == email);
        if (customer == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy tài khoản với địa chỉ email này.";
            return RedirectToAction("Register");
        }

        if (customer.IsEmailVerified)
        {
            TempData["InfoMessage"] = "Email của bạn đã được xác minh trước đó. Bạn có thể đăng nhập.";
            return RedirectToAction("Login");
        }

        // Vô hiệu hóa các OTP "CustomerAccountVerification" cũ chưa sử dụng và còn hạn cho CustomerID này
        var oldOtps = await _context.OtpRequests
            .Where(o => o.CustomerID == customer.CustomerID &&
                        o.OtpType.OtpTypeName == "CustomerAccountVerification" &&
                        !o.IsUsed && o.ExpirationTime > DateTime.UtcNow)
            .ToListAsync();

        foreach (var oldOtp in oldOtps)
        {
            oldOtp.IsUsed = true; // Đánh dấu là đã sử dụng để vô hiệu hóa
            oldOtp.Status = "InvalidatedByResend"; // Hoặc một trạng thái rõ ràng khác
        }

        // Tạo OTP mới
        string newOtpCode = GenerateOtp();
        var otpType = await _context.OtpTypes.FirstOrDefaultAsync(ot => ot.OtpTypeName == "CustomerAccountVerification");
        if (otpType == null) // Kiểm tra này vẫn quan trọng
        {
            _logger.LogError("CRITICAL: OtpType 'CustomerAccountVerification' not found for Resend OTP.");
            TempData["ErrorMessage"] = "Lỗi hệ thống: Không thể tạo yêu cầu xác minh mới.";
            TempData["VerificationEmail"] = customer.Email; // Giữ lại email cho trang VerifyOtp
            return RedirectToAction("VerifyOtp");
        }

        var otpRequest = new OtpRequest
        {
            CustomerID = customer.CustomerID,
            OtpTypeID = otpType.OtpTypeID,
            OtpCode = newOtpCode,
            RequestTime = DateTime.UtcNow,
            ExpirationTime = DateTime.UtcNow.AddMinutes(15),
            IsUsed = false,
            Status = "PendingVerification"
        };
        _context.OtpRequests.Add(otpRequest);

        try
        {
            await _context.SaveChangesAsync(); // Lưu các OTP cũ đã bị vô hiệu hóa và OTP mới

            string emailSubject = "Mã OTP xác minh MiniMart mới của bạn";
            string emailMessage = $"<p>Chào {customer.FirstName},</p>" +
                                  $"<p>Mã OTP mới của bạn để xác minh tài khoản MiniMart là: <strong>{newOtpCode}</strong></p>" +
                                  $"<p>Mã này sẽ hết hạn sau 15 phút.</p>";
            await _emailSender.SendEmailAsync(customer.Email, emailSubject, emailMessage);

            _logger.LogInformation("Mã OTP mới đã được gửi lại đến {Email} cho khách hàng {Username}.", customer.Email, customer.Username);
            TempData["VerificationEmail"] = customer.Email; // Để trang VerifyOtp biết email
            TempData["InfoMessage"] = "Một mã OTP mới đã được gửi đến email của bạn. Vui lòng kiểm tra.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gửi lại OTP cho {Email}.", customer.Email);
            TempData["VerificationEmail"] = customer.Email;
            TempData["ErrorMessage"] = "Đã xảy ra lỗi khi cố gắng gửi lại mã OTP. Vui lòng thử lại sau.";
        }

        return RedirectToAction("VerifyOtp"); // Luôn chuyển lại trang nhập OTP
    }


    // === KẾT THÚC CÁC ACTION CHO ĐĂNG KÝ VÀ XÁC MINH ===


    // --- Hàm tiện ích ---
    private bool VerifyPassword(string password, byte[] storedHash, byte[] storedSalt)
    {
        if (storedHash == null || storedSalt == null || storedHash.Length == 0 || storedSalt.Length == 0)
        {
            _logger.LogWarning("Xác minh mật khẩu thất bại: hash hoặc salt lưu trữ rỗng/null.");
            return false;
        }
        using var sha256 = SHA256.Create();
        var combinedBytes = Encoding.UTF8.GetBytes(password).Concat(storedSalt).ToArray();
        var computedHash = sha256.ComputeHash(combinedBytes);
        return storedHash.SequenceEqual(computedHash);
    }

    private (byte[] passwordHash, byte[] salt) GeneratePasswordHashAndSalt(string password)
    {
        var salt = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }
        using var sha256 = SHA256.Create();
        var combinedBytes = Encoding.UTF8.GetBytes(password).Concat(salt).ToArray();
        var computedHash = sha256.ComputeHash(combinedBytes);
        return (computedHash, salt);
    }

    private string GenerateOtp(int length = 6)
    {
        const string chars = "0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, length)
          .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    private ClaimsIdentity CreateClaimsIdentity(string username, string role, string userId)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role),
            new Claim(ClaimTypes.NameIdentifier, userId)
            // Có thể thêm các claim khác như Email, CustomerID/EmployeeID dưới một tên claim tùy chỉnh
            // new Claim("email", emailFromDatabase),
        };
        return new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    }

    private IActionResult RedirectBasedOnRole(ClaimsPrincipal user)
    {
        if (user.IsInRole("Admin") || user.IsInRole("Staff"))
        {
            // Chuyển hướng Admin/Staff đến trang quản trị của họ
            return RedirectToAction("Index", "Dashboard", new { area = "Admin" }); // Ví dụ: nếu có Area Admin
            // Hoặc return RedirectToAction("Index", "AdminDashboard"); // Nếu không có Area
        }
        else if (user.IsInRole("Customer"))
        {
            return RedirectToAction("Index", "Home"); // Khách hàng về trang chủ
        }
        return RedirectToAction("Index", "Home"); // Mặc định
    }

    private Dictionary<string, string[]> ModelStateToDictionary(ModelStateDictionary modelState)
    {
        return modelState
            .Where(x => x.Value != null && x.Value.Errors.Count > 0)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
            );
    }
}