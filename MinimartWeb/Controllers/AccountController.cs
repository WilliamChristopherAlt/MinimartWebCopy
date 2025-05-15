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
    // Trong AccountController.cs

    // ... (các using và constructor) ...

    // GET: /Account/EmployeeProfile
    // Trong AccountController.cs
    [Authorize(Roles = "Admin,Staff,Thu ngân,Quản lý kho,Nhân viên giao hàng,Giám sát,Quản trị viên")]
    [HttpGet]
    public async Task<IActionResult> EmployeeProfile()
    {
        _logger.LogInformation("GET /Account/EmployeeProfile accessed by User: {User}", User.Identity?.Name);

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int employeeIdFromClaims))
        {
            _logger.LogWarning("EmployeeProfile GET: Could not parse EmployeeID from claims for User: {User}", User.Identity?.Name);
            return Challenge();
        }

        _logger.LogInformation("EmployeeProfile GET: Attempting to load profile for EmployeeID: {EmployeeId}", employeeIdFromClaims);

        var employee = await _context.Employees
            .Include(e => e.Role)
            .Include(e => e.EmployeeAccount)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EmployeeID == employeeIdFromClaims);

        if (employee == null)
        {
            _logger.LogWarning("EmployeeProfile GET: Employee with ID {EmployeeId} not found.", employeeIdFromClaims);
            return NotFound($"Không tìm thấy thông tin nhân viên với ID: {employeeIdFromClaims}.");
        }

        if (employee.EmployeeAccount == null)
        {
            _logger.LogError("EmployeeProfile GET: EmployeeAccount is null for EmployeeID {EmployeeId}. Data inconsistency.", employeeIdFromClaims);
            return StatusCode(StatusCodes.Status500InternalServerError, "Lỗi dữ liệu: Không tìm thấy thông tin tài khoản đăng nhập của nhân viên.");
        }

        var viewModel = new EmployeeProfileViewModel
        {
            EmployeeId = employee.EmployeeID, // Gán EmployeeId để View có thể dùng nếu cần (ví dụ cho các form khác trên trang)
                                              // AccountId = employee.EmployeeAccount.AccountID, // Gán nếu ViewModel có
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
            Username = employee.EmployeeAccount.Username,
            IsAccountActive = employee.EmployeeAccount.IsActive,
            IsEmployeeEmailVerified = employee.EmployeeAccount.IsEmailVerified,
            EmployeeEmailVerifiedAt = employee.EmployeeAccount.EmailVerifiedAt,
            Is2FAEnabled = employee.EmployeeAccount.Is2FAEnabled // LẤY TRẠNG THÁI 2FA TỪ DB
        };

        _logger.LogInformation("HttpGet EmployeeProfile - ViewModel Is2FAEnabled being sent to View: {Is2FAEnabled}", viewModel.Is2FAEnabled);

        return View("EmployeeProfile", viewModel);
    }

    // Action POST ToggleEmployee2FA mà bạn đã cung cấp ở trên
    // Trong AccountController.cs

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleEmployee2FA(
        // Nhận trực tiếp từ form để tránh bind vào EmployeeProfileViewModel nếu không cần thiết
        [FromForm] bool enable, // Giá trị từ input hidden name="enable"
        [FromForm] string? passwordFor2FAChange // Giá trị từ input có name="PasswordForChange2FAStatus" (hoặc tên bạn đặt trong form 2FA)
    )
    {
        _logger.LogInformation("HttpPost ToggleEmployee2FA - Received 'enable' parameter: {EnableParam}, Received 'password': {PasswordProvided}", enable, !string.IsNullOrWhiteSpace(passwordFor2FAChange));
        var employeeIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(employeeIdClaim, out int currentEmployeeId))
        {
            TempData["ErrorMessage_2FA_Employee"] = "Phiên làm việc không hợp lệ.";
            _logger.LogWarning("ToggleEmployee2FA: User not authenticated or EmployeeID claim missing.");
            return RedirectToAction(nameof(EmployeeProfile));
        }

        if (string.IsNullOrWhiteSpace(passwordFor2FAChange))
        {
            TempData["ErrorMessage_2FA_Employee"] = "Vui lòng nhập mật khẩu hiện tại để xác nhận.";
            return RedirectToAction(nameof(EmployeeProfile));
        }

        var employeeAccount = await _context.EmployeeAccounts
                                    .FirstOrDefaultAsync(ea => ea.EmployeeID == currentEmployeeId);

        if (employeeAccount == null)
        {
            TempData["ErrorMessage_2FA_Employee"] = "Không tìm thấy tài khoản nhân viên.";
            _logger.LogWarning("ToggleEmployee2FA: EmployeeAccount not found for EID {EmployeeId}", currentEmployeeId);
            return RedirectToAction(nameof(EmployeeProfile));
        }

        if (!VerifyPassword(passwordFor2FAChange, employeeAccount.PasswordHash, employeeAccount.Salt))
        {
            TempData["ErrorMessage_2FA_Employee"] = "Mật khẩu hiện tại không đúng.";
            _logger.LogWarning("ToggleEmployee2FA: Incorrect password for EID {EmployeeId}", currentEmployeeId);
            return RedirectToAction(nameof(EmployeeProfile));
        }

        employeeAccount.Is2FAEnabled = enable; // Gán trạng thái mới từ form
        _context.EmployeeAccounts.Update(employeeAccount);

        try
        {
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Xác thực hai yếu tố qua email đã được {(enable ? "BẬT" : "TẮT")} thành công!";
            _logger.LogInformation("EID {EmployeeId} updated 2FA status to: {Status}", currentEmployeeId, enable);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating 2FA status for EID {EmployeeId}", currentEmployeeId);
            TempData["ErrorMessage_2FA_Employee"] = "Đã có lỗi xảy ra khi lưu thay đổi cài đặt 2FA.";
        }

        return RedirectToAction(nameof(EmployeeProfile));
    }

    // GET: /Account/Settings
    [Authorize(Roles = "Customer")]
    [HttpGet]
    public async Task<IActionResult> Settings()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int customerId))
        {
            _logger.LogWarning("Settings GET: User not authenticated or CustomerID claim is missing/invalid.");
            return Challenge();
        }

        var customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerID == customerId);
        if (customer == null)
        {
            _logger.LogWarning("Settings GET: Customer with ID {CustomerId} not found.", customerId);
            return NotFound("Tài khoản không tồn tại.");
        }

        var viewModel = new CustomerSettingsViewModel
        {
            CustomerId = customer.CustomerID, // Vẫn gán vào ViewModel để View có thể dùng nếu cần (ví dụ cho các link khác)
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            Email = customer.Email, // Thường là readonly trên form này
            PhoneNumber = customer.PhoneNumber,
            Username = customer.Username, // Thường là readonly
            ImagePath = customer.ImagePath,
            Is2FAEnabled = customer.Is2FAEnabled
        };
        return View(viewModel);
    }

    // POST: /Account/Settings (Cập nhật thông tin cá nhân)
    // Trong AccountController.cs

    [Authorize(Roles = "Customer")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Settings(CustomerSettingsViewModel model)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int currentUserIdFromClaims))
        {
            _logger.LogWarning("Settings POST: User not authenticated or CustomerID claim is missing/invalid. User: {User}", User.Identity?.Name);
            return Forbid();
        }

        var customerToUpdate = await _context.Customers.FirstOrDefaultAsync(c => c.CustomerID == currentUserIdFromClaims);
        if (customerToUpdate == null)
        {
            _logger.LogWarning("Settings POST: Customer with ID {CustomerId} (from claims) not found for update.", currentUserIdFromClaims);
            return NotFound("Tài khoản không tồn tại để cập nhật.");
        }

        // Gán lại các giá trị không được phép sửa và trạng thái 2FA vào model để View hiển thị đúng nếu có lỗi
        model.Email = customerToUpdate.Email;
        model.Username = customerToUpdate.Username;
        model.CustomerId = customerToUpdate.CustomerID;
        model.Is2FAEnabled = customerToUpdate.Is2FAEnabled; // QUAN TRỌNG: Gán lại trạng thái 2FA
        if (string.IsNullOrEmpty(model.ImagePath) && model.NewImageFile == null)
        {
            model.ImagePath = customerToUpdate.ImagePath;
        }

        ModelState.Remove(nameof(model.Email));
        ModelState.Remove(nameof(model.Username));
        ModelState.Remove(nameof(model.Is2FAEnabled)); // Is2FAEnabled không phải là input của form này
        ModelState.Remove(nameof(model.PasswordForChange2FAStatus)); // Password này thuộc form 2FA

        bool hasProfileInfoChanges = customerToUpdate.FirstName != model.FirstName ||
                                     customerToUpdate.LastName != model.LastName ||
                                     customerToUpdate.PhoneNumber != model.PhoneNumber ||
                                     (model.NewImageFile != null && model.NewImageFile.Length > 0);

        if (hasProfileInfoChanges && string.IsNullOrWhiteSpace(model.CurrentPassword))
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), "Vui lòng nhập mật khẩu hiện tại để lưu các thay đổi thông tin cá nhân.");
        }

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Settings POST: ModelState is invalid for CustomerID: {CustomerId}. Errors: {@Errors}", currentUserIdFromClaims, ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)));
            return View(model); // Trả về View với các lỗi validation
        }

        if (hasProfileInfoChanges)
        {
            if (!VerifyPassword(model.CurrentPassword!, customerToUpdate.PasswordHash, customerToUpdate.Salt))
            {
                ModelState.AddModelError(nameof(model.CurrentPassword), "Mật khẩu hiện tại không đúng.");
                _logger.LogWarning("Settings POST: Incorrect current password for CustomerID: {CustomerId}", currentUserIdFromClaims);
                return View(model);
            }
        }
        else // Không có thay đổi thông tin nào
        {
            TempData["InfoMessage"] = "Không có thông tin cá nhân nào được thay đổi.";
            return RedirectToAction(nameof(Settings));
        }

        // Cập nhật các trường được phép thay đổi
        customerToUpdate.FirstName = model.FirstName;
        customerToUpdate.LastName = model.LastName;
        customerToUpdate.PhoneNumber = model.PhoneNumber;

        if (model.NewImageFile != null && model.NewImageFile.Length > 0)
        {
            // ... (Logic upload ảnh giữ nguyên như trước) ...
            try { /* ... */ } catch (Exception ex) { /* ... ModelState.AddModelError ... return View(model); */ }
        }

        try
        {
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Thông tin cá nhân đã được cập nhật thành công!";
            _logger.LogInformation("Settings POST: Thông tin CustomerID {CustomerId} đã được cập nhật vào DB.", currentUserIdFromClaims);
            return RedirectToAction(nameof(Settings));
        }
        catch (DbUpdateException ex) { /* ... xử lý lỗi DB ... */ }
        // ... (các catch khác) ...

        return View(model);
    }


    // Action POST để Bật/Tắt 2FA (từ trang Settings)
    [Authorize(Roles = "Customer")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCustomer2FA( // Giữ nguyên tên action này nếu View đang trỏ đến nó
        [FromForm] bool enable, // Nhận trực tiếp trạng thái mới từ input hidden "enable"
        [FromForm] string? passwordForChange2FAStatus // Nhận mật khẩu từ input có name="PasswordForChange2FAStatus"
                                                      // Không cần truyền cả CustomerSettingsViewModel nếu chỉ cần 2 trường này
    )
    {
        var customerIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(customerIdClaim, out int currentCustomerId))
        {
            TempData["ErrorMessage_2FA_Settings"] = "Phiên làm việc không hợp lệ.";
            _logger.LogWarning("ToggleCustomer2FA: User not authenticated or CustomerID claim missing.");
            return RedirectToAction(nameof(Settings));
        }

        if (string.IsNullOrWhiteSpace(passwordForChange2FAStatus))
        {
            TempData["ErrorMessage_2FA_Settings"] = "Vui lòng nhập mật khẩu hiện tại để thay đổi cài đặt 2FA.";
            return RedirectToAction(nameof(Settings));
        }

        var customer = await _context.Customers.FindAsync(currentCustomerId);
        if (customer == null)
        {
            TempData["ErrorMessage_2FA_Settings"] = "Không tìm thấy tài khoản khách hàng.";
            return RedirectToAction(nameof(Settings));
        }

        if (!VerifyPassword(passwordForChange2FAStatus, customer.PasswordHash, customer.Salt))
        {
            TempData["ErrorMessage_2FA_Settings"] = "Mật khẩu hiện tại không đúng.";
            return RedirectToAction(nameof(Settings));
        }

        customer.Is2FAEnabled = enable; // Gán trạng thái mới từ form
        _context.Customers.Update(customer);

        try
        {
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Xác thực hai yếu tố đã được {(enable ? "BẬT" : "TẮT")} thành công!";
            _logger.LogInformation("CustomerID {CustomerId} updated 2FA status to: {Status} via ToggleCustomer2FA action.", currentCustomerId, enable);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating 2FA status for CustomerID {CustomerId} via ToggleCustomer2FA action.", currentCustomerId);
            TempData["ErrorMessage_2FA_Settings"] = "Đã có lỗi xảy ra khi lưu thay đổi cài đặt 2FA.";
        }

        return RedirectToAction(nameof(Settings));
    }

    // File: Controllers/AccountController.cs

    // ... (using statements và khai báo class, constructor như bạn đã có) ...

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Login POST: ModelState invalid. User: {Username}, Type: {UserType}", model.Username, model.UserType);
            var errors = ModelState.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray());
            return BadRequest(new { success = false, errors = errors, message = "Dữ liệu đầu vào không hợp lệ." });
        }

        _logger.LogInformation("Login POST: Attempting login for User: {Username}, Type: {UserType}", model.Username, model.UserType);

        string userEmailForOtp = string.Empty; // Sẽ được gán nếu cần gửi OTP
        int? customerIdForRecord = null;
        int? employeeAccountIdForRecord = null;
        bool is2FAEnabledForThisUser = false;
        ClaimsIdentity? identity = null;       // Sẽ được tạo nếu đăng nhập trực tiếp hoặc sau OTP
        string roleToUseInClaims = string.Empty; // Vai trò sẽ dùng để tạo claim
        string userIdForClaims = string.Empty;   // ID người dùng cho claim
        string usernameForDisplayAndClaims = model.Username; // Sẽ được override bằng username từ DB

        try
        {
            if (model.UserType == "Customer")
            {
                var customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Username == model.Username);
                if (customer == null || !VerifyPassword(model.Password, customer.PasswordHash, customer.Salt))
                {
                    _logger.LogWarning("Login failed (Customer): Invalid credentials for {Username}", model.Username);
                    return BadRequest(new { success = false, message = "Tên đăng nhập hoặc mật khẩu không đúng." });
                }

                if (!customer.IsEmailVerified)
                { /* ... xử lý email chưa xác minh ... */ return BadRequest(new { success = false, message = "Tài khoản chưa xác minh email.", needsInitialVerification = true, email = customer.Email }); }

                userEmailForOtp = customer.Email;
                customerIdForRecord = customer.CustomerID;
                is2FAEnabledForThisUser = customer.Is2FAEnabled;
                usernameForDisplayAndClaims = customer.Username;
                roleToUseInClaims = "Customer"; // Vai trò cố định
                userIdForClaims = customer.CustomerID.ToString();

                _logger.LogInformation("Customer {Username} authenticated. 2FA Enabled: {Is2FAEnabled}", usernameForDisplayAndClaims, is2FAEnabledForThisUser);
            }
            // 👉 Employee login
            else if (model.UserType == "Employee")
            {
                var account = await _context.EmployeeAccounts.AsNoTracking()
                    .Include(ea => ea.Employee) // Vẫn cần Employee để lấy Email
                                                // .ThenInclude(e => e!.Role) // Không cần Include Role nữa nếu không lấy RoleName
                    .FirstOrDefaultAsync(ea => ea.Username == model.Username);

                if (account == null || !VerifyPassword(model.Password, account.PasswordHash, account.Salt))
                {
                    _logger.LogWarning("Login failed (Employee): Invalid credentials for {Username}", model.Username);
                    return BadRequest(new { success = false, message = "Tên đăng nhập hoặc mật khẩu không đúng." });
                }

                // 🔥 **Role Assignment Logic**
                if (account.Employee.Role.RoleName == "Quản trị viên")
                {
                    role = "Admin";
                }
                else
                {
                    role = "Staff";
                }

                displayName = account.Username;
                if (account.Employee == null)
                { /* ... lỗi dữ liệu ... */ return StatusCode(500, new { success = false, message = "Lỗi dữ liệu hệ thống (E01)." }); }
                if (!account.IsActive)
                { /* ... tài khoản khóa ... */ return BadRequest(new { success = false, message = "Tài khoản nhân viên đã bị khóa." }); }
                if (!account.IsEmailVerified)
                { /* ... email chưa xác minh ... */ return BadRequest(new { success = false, message = "Email tài khoản nhân viên chưa xác minh.", needsEmployeeEmailVerification = true, emailForVerification = account.Employee.Email }); }

                userEmailForOtp = account.Employee.Email;
                employeeAccountIdForRecord = account.AccountID;
                is2FAEnabledForThisUser = account.Is2FAEnabled;
                usernameForDisplayAndClaims = account.Username;
                roleToUseInClaims = "Admin"; // <<== GÁN CỨNG VAI TRÒ "Admin" CHO TẤT CẢ NHÂN VIÊN
                userIdForClaims = account.EmployeeID.ToString();

                _logger.LogInformation("Employee {Username} authenticated. 2FA Enabled: {Is2FAEnabled}. Role will be set as '{StaticRole}'", usernameForDisplayAndClaims, is2FAEnabledForThisUser, roleToUseInClaims);
            }
            else
            {
                _logger.LogWarning("Login: Invalid UserType: {UserType}", model.UserType);
                return BadRequest(new { success = false, message = "Loại người dùng không hợp lệ." });
            }

            // --- Bước 2: Quyết định luồng dựa trên trạng thái Is2FAEnabled ---
            if (is2FAEnabledForThisUser)
            {
                // --- Người dùng ĐÃ BẬT 2FA: Tiến hành gửi OTP qua Email ---
                _logger.LogInformation("Account {Username} ({UserType}) has 2FA enabled. Sending OTP.", usernameForDisplayAndClaims, model.UserType);

                if (string.IsNullOrEmpty(userEmailForOtp))
                { /* ... lỗi không có email ... */ return StatusCode(500, new { success = false, message = "Lỗi hệ thống: Không thể gửi mã xác thực." }); }

                string otpCode = GenerateOtp();
                var otpType = await _context.OtpTypes.FirstOrDefaultAsync(ot => ot.OtpTypeName == "LoginTwoFactorVerification");
                if (otpType == null) { /* ... lỗi config OTP Type ... */ return StatusCode(500, new { success = false, message = "Lỗi cấu hình hệ thống (OTP Type)." }); }

                // ... (Logic vô hiệu hóa OTP cũ và tạo OtpRequest mới như cũ) ...
                var existingUnusedOtpsQuery = _context.OtpRequests.Where(o => o.OtpTypeID == otpType.OtpTypeID && !o.IsUsed && o.ExpirationTime > DateTime.UtcNow);
                if (customerIdForRecord.HasValue) existingUnusedOtpsQuery = existingUnusedOtpsQuery.Where(o => o.CustomerID == customerIdForRecord.Value);
                else if (employeeAccountIdForRecord.HasValue) existingUnusedOtpsQuery = existingUnusedOtpsQuery.Where(o => o.EmployeeAccountID == employeeAccountIdForRecord.Value);
                var oldOtpsToInvalidate = await existingUnusedOtpsQuery.ToListAsync();
                oldOtpsToInvalidate.ForEach(o => { o.IsUsed = true; o.Status = "InvalidatedByNewLoginAttempt"; });

                var otpRequest = new OtpRequest { /* ... gán các giá trị ... OtpCode = otpCode ... */ CustomerID = customerIdForRecord, EmployeeAccountID = employeeAccountIdForRecord, OtpTypeID = otpType.OtpTypeID, OtpCode = otpCode, RequestTime = DateTime.UtcNow, ExpirationTime = DateTime.UtcNow.AddMinutes(5), IsUsed = false, Status = "PendingLogin2FAEmail" };
                _context.OtpRequests.Add(otpRequest);
                await _context.SaveChangesAsync();

                string emailSubject = "MiniMart - Mã Xác Thực Đăng Nhập";
                string emailMessage = $"<p>Xin chào {usernameForDisplayAndClaims},</p><p>Mã xác thực đăng nhập của bạn là: <strong>{otpCode}</strong>...</p>";
                await _emailSender.SendEmailAsync(userEmailForOtp, emailSubject, emailMessage);
                _logger.LogInformation("2FA OTP sent for {Username} ({UserType}).", usernameForDisplayAndClaims, model.UserType);

                // Lưu thông tin cần thiết vào TempData (BAO GỒM CẢ ROLE ĐÃ GÁN CỨNG)
                TempData["2FA_Attempt_Username"] = usernameForDisplayAndClaims;
                TempData["2FA_Attempt_UserType"] = model.UserType;
                TempData["2FA_Attempt_EmailForDisplay"] = userEmailForOtp;
                TempData["2FA_Attempt_RememberMe"] = model.RememberMe;
                TempData["2FA_Attempt_Role"] = roleToUseInClaims; // Vai trò đã gán (ví dụ: "Admin" cho Employee)
                TempData["2FA_Attempt_UserId"] = userIdForClaims;

                return Ok(new { success = true, needsOtpVerification = true, redirectUrl = Url.Action(nameof(VerifyLoginOtp)) });
            }
            else
            {
                // --- Người dùng KHÔNG BẬT 2FA: Đăng nhập trực tiếp ---
                _logger.LogInformation("Account {Username} ({UserType}) does not require 2FA. Signing in directly.", usernameForDisplayAndClaims, model.UserType);

                if (string.IsNullOrEmpty(roleToUseInClaims) || string.IsNullOrEmpty(userIdForClaims))
                { /* ... lỗi thiếu thông tin claims ... */ return StatusCode(500, new { success = false, message = "Lỗi hệ thống." }); }

                identity = new ClaimsIdentity(new[] // Tạo identity ở đây
                {
                new Claim(ClaimTypes.NameIdentifier, userIdForClaims),
                new Claim(ClaimTypes.Name, usernameForDisplayAndClaims),
                new Claim(ClaimTypes.Role, roleToUseInClaims) // Sử dụng role đã gán
            }, CookieAuthenticationDefaults.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role);

                var principal = new ClaimsPrincipal(identity);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe
                };
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

                _logger.LogInformation("User {Username} ({UserType}) signed in directly with role {Role}.", usernameForDisplayAndClaims, model.UserType, roleToUseInClaims);
                return Ok(new { success = true, redirectUrl = Url.Action("Index", "Home") });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi không mong muốn trong quá trình Login cho {Username}, Type: {UserType}. Message: {ExMsg}", model.Username, model.UserType, ex.Message);
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


    // Trong AccountController.cs

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EmployeeProfile(EmployeeProfileViewModel model)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier); // EmployeeID từ claims
        if (!int.TryParse(userIdString, out int currentEmployeeId))
        {
            _logger.LogWarning("EmployeeProfile POST: User not authenticated or EmployeeID claim missing.");
            return Forbid();
        }

        // Gán lại EmployeeId từ Claims vào model, vì input ẩn có thể đã bị bỏ
        model.EmployeeId = currentEmployeeId;

        var employeeInDb = await _context.Employees
                                .Include(e => e.EmployeeAccount) // Cần EmployeeAccount để xác thực mật khẩu
                                .FirstOrDefaultAsync(e => e.EmployeeID == currentEmployeeId);

        if (employeeInDb == null || employeeInDb.EmployeeAccount == null)
        {
            _logger.LogError("EmployeeProfile POST: Employee or EmployeeAccount not found for ID from claims: {EmployeeId}", currentEmployeeId);
            return NotFound("Tài khoản nhân viên không tồn tại hoặc thiếu thông tin tài khoản đăng nhập.");
        }

        // Gán lại các giá trị không được phép sửa từ DB vào model để ModelState và View hiển thị đúng nếu có lỗi
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
        model.Is2FAEnabled = employeeInDb.EmployeeAccount.Is2FAEnabled; // Giữ trạng thái 2FA hiện tại

        if (string.IsNullOrEmpty(model.ImagePath) && model.NewImageFile == null) model.ImagePath = employeeInDb.ImagePath;

        bool infoAttemptedToChange = (employeeInDb.PhoneNumber != model.PhoneNumber || model.NewImageFile != null);

        if (infoAttemptedToChange && string.IsNullOrEmpty(model.CurrentPasswordForUpdate))
        {
            ModelState.AddModelError("CurrentPasswordForUpdate", "Vui lòng nhập mật khẩu hiện tại để xác nhận thay đổi thông tin.");
        }

        if (ModelState.IsValid)
        {
            if (infoAttemptedToChange)
            {
                if (!VerifyPassword(model.CurrentPasswordForUpdate!, employeeInDb.EmployeeAccount.PasswordHash, employeeInDb.EmployeeAccount.Salt))
                {
                    ModelState.AddModelError("CurrentPasswordForUpdate", "Mật khẩu hiện tại không đúng.");
                    _logger.LogWarning("EmployeeProfile POST: Incorrect current password for EID: {EmployeeId}", currentEmployeeId);
                    model.ImagePath = employeeInDb.ImagePath; // Giữ ảnh cũ khi hiển thị lại form nếu có lỗi
                    return View("EmployeeProfile", model);
                }
            }

            bool actualChangesMadeToDb = false;

            if (employeeInDb.PhoneNumber != model.PhoneNumber)
            {
                // Kiểm tra UNIQUE cho PhoneNumber (nếu cần)
                // ...
                employeeInDb.PhoneNumber = model.PhoneNumber;
                actualChangesMadeToDb = true;
            }

            if (model.NewImageFile != null && model.NewImageFile.Length > 0)
            {
                // ... (Logic upload ảnh của bạn) ...
                // Ví dụ:
                try
                {
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "employees");
                    // ... (xóa ảnh cũ, lưu ảnh mới, cập nhật employeeInDb.ImagePath) ...
                    actualChangesMadeToDb = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi upload ảnh EmployeeProfile cho EID: {EmployeeId}", currentEmployeeId);
                    ModelState.AddModelError("NewImageFile", "Lỗi tải lên ảnh đại diện.");
                    model.ImagePath = employeeInDb.ImagePath;
                    return View("EmployeeProfile", model);
                }
            }

            if (actualChangesMadeToDb)
            {
                try
                {
                    _context.Employees.Update(employeeInDb);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Thông tin cá nhân đã được cập nhật!";
                    return RedirectToAction(nameof(EmployeeProfile));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "EmployeeProfile POST: Lỗi DB khi cập nhật EID: {EmployeeId}", currentEmployeeId);
                    ModelState.AddModelError(string.Empty, "Lỗi cập nhật cơ sở dữ liệu.");
                }
            }
            else if (infoAttemptedToChange) // Có ý định thay đổi nhưng không có gì thực sự khác
            {
                TempData["InfoMessage"] = "Không có thông tin nào thực sự thay đổi.";
                return RedirectToAction(nameof(EmployeeProfile));
            }
            else // Không có ý định thay đổi gì (chỉ nhấn nút "Lưu" mà không sửa gì)
            {
                TempData["InfoMessage"] = "Không có thay đổi nào được thực hiện.";
                return RedirectToAction(nameof(EmployeeProfile));
            }
        }

        // Nếu ModelState không hợp lệ
        if (string.IsNullOrEmpty(model.ImagePath) && employeeInDb != null) model.ImagePath = employeeInDb.ImagePath;
        return View("EmployeeProfile", model);
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
            Username = customer.Username,
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
                .SelectMany(s => s.SaleDetails.Select(sd => new
                {
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

    // using System.Security.Claims;
    // using Microsoft.AspNetCore.Authorization;
    // using Microsoft.AspNetCore.Mvc;
    // using Microsoft.EntityFrameworkCore;
    // using MinimartWeb.Data;
    // using MinimartWeb.Models;
    // using MinimartWeb.Services; // Giả sử IEmailSender ở đây


    // --- ACTION HIỂN THỊ FORM YÊU CẦU ĐỔI EMAIL ---
    [Authorize(Roles = "Customer")]
    [HttpGet]
    public async Task<IActionResult> RequestChangeEmail()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int customerId)) { _logger.LogWarning("RequestChangeEmail GET: Unauthenticated or invalid CID claim."); return Challenge(); }

        var customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerID == customerId);
        if (customer == null) { _logger.LogWarning("RequestChangeEmail GET: Customer ID {CID} not found.", customerId); return NotFound("Tài khoản không tồn tại."); }

        var viewModel = new RequestChangeEmailViewModel { CurrentEmail = customer.Email };
        _logger.LogInformation("RequestChangeEmail GET: Displaying form for CID {CID}, CurrentEmail: {Email}", customerId, customer.Email);
        return View(viewModel);
    }

    // --- ACTION XỬ LÝ YÊU CẦU ĐỔI EMAIL (BƯỚC 1: GỬI OTP ĐẾN EMAIL CŨ) ---
    [Authorize(Roles = "Customer")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestChangeEmail(RequestChangeEmailViewModel model)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int customerId)) { _logger.LogWarning("RequestChangeEmail POST: Unauthenticated or invalid CID claim."); return Challenge(); }

        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.CustomerID == customerId);
        if (customer == null) { _logger.LogWarning("RequestChangeEmail POST: Customer ID {CID} not found.", customerId); return NotFound("Tài khoản không tồn tại."); }

        model.CurrentEmail = customer.Email; // Gán lại để hiển thị trên view nếu có lỗi
        if (!ModelState.IsValid) { _logger.LogWarning("RequestChangeEmail POST: ModelState invalid for CID {CID}.", customerId); return View(model); }

        if (!VerifyPassword(model.CurrentPassword, customer.PasswordHash, customer.Salt))
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), "Mật khẩu hiện tại không đúng.");
            _logger.LogWarning("RequestChangeEmail POST: Incorrect password for CID {CID}.", customerId);
            return View(model);
        }

        var newEmailNormalized = model.NewEmail.Trim().ToLower();
        // Kiểm tra NotEqualTo đã xử lý việc email mới giống email cũ
        // if (string.Equals(newEmailNormalized, customer.Email.ToLowerInvariant())) { ModelState.AddModelError(nameof(model.NewEmail), "Email mới phải khác email cũ."); return View(model); }

        if (await _context.Customers.AnyAsync(c => c.Email.ToLower() == newEmailNormalized && c.CustomerID != customerId))
        {
            ModelState.AddModelError(nameof(model.NewEmail), "Email mới đã được sử dụng bởi tài khoản khác.");
            _logger.LogWarning("RequestChangeEmail POST: New email {NewEmail} already in use. CID {CID}", newEmailNormalized, customerId);
            return View(model);
        }

        // BƯỚC 1: Gửi OTP đến EMAIL CŨ để xác nhận yêu cầu
        var otpTypeNameForOldEmail = "CustomerConfirmEmailChangeRequest"; // Loại OTP cho email cũ
        var otpTypeOld = await _context.OtpTypes.FirstOrDefaultAsync(ot => ot.OtpTypeName == otpTypeNameForOldEmail);
        if (otpTypeOld == null)
        {
            _logger.LogCritical("CRITICAL: OtpType '{OtpT}' not found for confirming email change request.", otpTypeNameForOldEmail);
            ModelState.AddModelError(string.Empty, "Lỗi hệ thống (OTP-CER). Vui lòng thử lại sau.");
            return View(model);
        }

        // Vô hiệu hóa các OTP "CustomerConfirmEmailChangeRequest" chưa dùng của user này
        var existingConfirmOtps = await _context.OtpRequests
            .Where(o => o.CustomerID == customerId && o.OtpTypeID == otpTypeOld.OtpTypeID && !o.IsUsed && o.ExpirationTime > DateTime.UtcNow)
            .ToListAsync();
        existingConfirmOtps.ForEach(o => { o.IsUsed = true; o.Status = $"InvalidatedByNewReqCE_To:{newEmailNormalized.Substring(0, Math.Min(newEmailNormalized.Length, 20))}"; }); // Giới hạn độ dài status

        string otpCodeForOldEmail = GenerateOtp();
        var otpRequestForOldEmail = new OtpRequest
        {
            CustomerID = customer.CustomerID,
            OtpTypeID = otpTypeOld.OtpTypeID,
            OtpCode = otpCodeForOldEmail,
            RequestTime = DateTime.UtcNow,
            ExpirationTime = DateTime.UtcNow.AddMinutes(10), // Thời gian ngắn hơn cho OTP xác nhận yêu cầu
            IsUsed = false,
            // Status này sẽ được kiểm tra khi xác minh OTP từ email cũ
            Status = $"ConfirmChangeReqTo:{newEmailNormalized.ToLower()}" // Quan trọng: Lưu email mới dự kiến
        };
        _context.OtpRequests.Add(otpRequestForOldEmail);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "DbUpdateException while saving OTP for old email confirmation. CID {CID}", customerId);
            // Kiểm tra InnerException để xem có phải lỗi truncated không
            if (ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx && sqlEx.Number == 2628)
            {
                ModelState.AddModelError(string.Empty, "Lỗi hệ thống: Không thể lưu thông tin OTP (Status quá dài). Vui lòng liên hệ hỗ trợ.");
                _logger.LogError("TRUNCATION ERROR for OtpRequest.Status. Value: {StatusValue}", otpRequestForOldEmail.Status);
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Lỗi hệ thống khi tạo OTP. Vui lòng thử lại.");
            }
            return View(model);
        }


        // Lưu email mới vào TempData để bước tiếp theo sử dụng sau khi OTP cũ được xác nhận
        TempData["PendingNewEmail_AfterOldEmailConfirmation"] = newEmailNormalized;
        TempData["ConfirmChangeEmail_CustomerId_ForOldOtp"] = customer.CustomerID.ToString(); // Để chắc chắn đúng user

        // Gửi email chứa OTP đến ĐỊA CHỈ EMAIL CŨ
        string emailSubjectOld = "MiniMart - Xác nhận Yêu cầu Thay đổi Địa chỉ Email";
        string emailMessageOld = $@"
            <p>Xin chào {customer.FirstName},</p>
            <p>Chúng tôi nhận được yêu cầu thay đổi địa chỉ email liên kết với tài khoản MiniMart của bạn (Tên đăng nhập: {customer.Username}) từ <strong>{customer.Email}</strong> thành <strong>{newEmailNormalized}</strong>.</p>
            <p>Để xác nhận rằng chính bạn đã thực hiện yêu cầu này, vui lòng sử dụng mã OTP sau (Mã này được gửi đến email hiện tại của bạn: {customer.Email}):</p>
            <p style='font-size: 1.5em; font-weight: bold; text-align: center;'>{otpCodeForOldEmail}</p>
            <p>Mã này sẽ hết hạn sau 10 phút.</p>
            <p>Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email này.</p>
            <p>Trân trọng,<br/>Đội ngũ MiniMart</p>";

        await _emailSender.SendEmailAsync(customer.Email, emailSubjectOld, emailMessageOld); // Gửi đến customer.Email (email CŨ)
        _logger.LogInformation("OTP to confirm email change request sent to OLD email {OldEmail} for CID {CID}. New email pending: {NewEmail}", customer.Email, customerId, newEmailNormalized);

        // Chuẩn bị cho trang VerifyOtpForAction
        TempData["OtpVerification_EmailToDisplay"] = customer.Email; // Hiển thị email đang nhận OTP
        TempData["OtpVerification_Purpose"] = "ConfirmEmailChangeRequest_FromOldEmail"; // Purpose cho bước 1
        string detailMessageForOldOtp = $"Một mã OTP đã được gửi đến email hiện tại của bạn (<strong>{customer.Email}</strong>) để xác nhận yêu cầu thay đổi email thành <strong>{newEmailNormalized}</strong>. Vui lòng kiểm tra và nhập mã OTP.";
        TempData["OtpVerification_DetailMessage"] = detailMessageForOldOtp;


        // Chuyển hướng đến trang nhập OTP chung, truyền purpose và detail
        return RedirectToAction(nameof(VerifyOtpForAction));
    }


    // --- ACTION GET HIỂN THỊ TRANG NHẬP OTP CHUNG ---
    [Authorize(Roles = "Customer")]
    [HttpGet]
    public IActionResult VerifyOtpForAction() // Không cần truyền tham số từ route nữa nếu dùng TempData
    {
        var purpose = TempData["OtpVerification_Purpose"] as string;
        var detailMessage = TempData["OtpVerification_DetailMessage"] as string;
        var emailForDisplay = TempData["OtpVerification_EmailToDisplay"] as string; // Email mà OTP đã được gửi đến

        _logger.LogInformation("VerifyOtpForAction GET - TempData received: Purpose='{Purpose}', EmailToDisplay='{Email}', DetailMessage='{Detail}'", purpose, emailForDisplay, detailMessage);


        if (string.IsNullOrEmpty(purpose))
        {
            _logger.LogWarning("VerifyOtpForAction GET: Purpose is missing from TempData. Redirecting to Home.");
            TempData["ErrorMessage"] = "Yêu cầu không hợp lệ hoặc phiên đã hết hạn.";
            return RedirectToAction("Index", "Home");
        }

        // Giữ lại các TempData cần thiết cho lần POST hoặc refresh
        TempData.Keep("OtpVerification_Purpose");
        TempData.Keep("OtpVerification_DetailMessage");
        TempData.Keep("OtpVerification_EmailToDisplay");
        TempData.Keep("LastVerificationDetail"); // Nếu bạn dùng key này

        // Giữ lại các TempData cụ thể cho từng purpose
        KeepSpecificTempDataForPurpose(purpose);


        var viewModel = new VerifyOtpGeneralViewModel
        {
            Purpose = purpose,
            VerificationDetail = detailMessage, // Sẽ được hiển thị trên view
            Email = emailForDisplay // Có thể dùng để hiển thị trên view nếu cần
        };

        // Cấu hình ViewBag cho View dựa trên purpose
        ViewBag.PageTitle = "Xác thực OTP"; // Mặc định
        ViewBag.SubmitButtonText = "Xác nhận";
        ViewBag.BackLinkAction = "Index";
        ViewBag.BackLinkController = "Home";
        ViewBag.BackLinkText = "Quay về Trang chủ";
        ViewBag.CardHeaderClass = "bg-primary text-white";


        if (purpose == "ConfirmEmailChangeRequest_FromOldEmail")
        {
            ViewBag.PageTitle = "Xác nhận Yêu cầu Đổi Email";
            // VerificationDetail đã có thông tin email cũ và mới
            ViewBag.SubmitButtonText = "Xác nhận Yêu cầu";
            ViewBag.BackLinkAction = nameof(RequestChangeEmail);
            ViewBag.BackLinkController = "Account";
            ViewBag.BackLinkText = "Hủy và Yêu cầu lại";
            ViewBag.CardHeaderClass = "bg-info text-white";
        }
        else if (purpose == "VerifyNewEmailAddress")
        {
            ViewBag.PageTitle = "Xác minh Địa chỉ Email Mới";
            // VerificationDetail sẽ được set khi chuyển từ bước 1 sang bước 2
            ViewBag.SubmitButtonText = "Xác minh Email Mới";
            ViewBag.BackLinkAction = nameof(Settings); // Hoặc Profile
            ViewBag.BackLinkController = "Account";
            ViewBag.BackLinkText = "Hủy và về Cài đặt";
            ViewBag.CardHeaderClass = "bg-success text-white";
        }
        // ... (Thêm các case khác cho ChangePassword, InitialEmailVerification, etc.)

        TempData["LastVerificationDetail"] = detailMessage; // Lưu lại để dùng nếu POST thất bại và return View
        _logger.LogInformation("VerifyOtpForAction GET: Displaying OTP page. ViewModel Purpose: {VmPurpose}, Email: {VmEmail}", viewModel.Purpose, viewModel.Email);

        return View("VerifyOtpGeneral", viewModel); // Sử dụng view chung VerifyOtpGeneral.cshtml
    }

    // --- ACTION POST XỬ LÝ OTP CHO NHIỀU MỤC ĐÍCH ---
    // --- ACTION POST XỬ LÝ OTP CHO NHIỀU MỤC ĐÍCH ---
    [Authorize(Roles = "Customer")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOtpForAction(VerifyOtpGeneralViewModel model)
    {
        // Lấy lại thông tin từ TempData để đảm bảo tính toàn vẹn
        var originalPurpose = TempData["OtpVerification_Purpose"] as string ?? model.Purpose;
        model.Purpose = originalPurpose;
        model.Email = TempData["OtpVerification_EmailToDisplay"] as string ?? model.Email;
        model.VerificationDetail = !string.IsNullOrEmpty(model.VerificationDetail)
                                   ? model.VerificationDetail
                                   : (TempData["LastVerificationDetail"] as string ?? TempData["OtpVerification_DetailMessage"] as string);

        _logger.LogInformation("VerifyOtpForAction POST - Received: Model.Purpose='{ModelPurpose}', Model.OtpCode='{OtpCode}', OriginalPurpose from TempData='{OriginalPurpose}'", model.Purpose, model.OtpCode, originalPurpose);

        // Giữ lại TempData cho trường hợp ModelState invalid và return View()
        TempData.Keep("OtpVerification_Purpose");
        TempData.Keep("OtpVerification_DetailMessage");
        TempData.Keep("OtpVerification_EmailToDisplay");
        TempData.Keep("LastVerificationDetail");
        KeepSpecificTempDataForPurpose(originalPurpose); // Giữ các key TempData cụ thể của purpose hiện tại

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("VerifyOtpForAction POST: ModelState invalid. Purpose: {Purpose}. Errors: {Errors}", model.Purpose, ModelStateErrorsToString(ModelState));
            // View "VerifyOtpGeneral.cshtml" nên tự quyết định hiển thị dựa trên Model.Purpose
            return View("VerifyOtpGeneral", model);
        }

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int currentUserId))
        {
            ModelState.AddModelError(string.Empty, "Lỗi xác thực người dùng.");
            _logger.LogWarning("VerifyOtpForAction POST: Unauthenticated or invalid CID claim. Purpose: {Purpose}", model.Purpose);
            return View("VerifyOtpGeneral", model);
        }

        // --- XỬ LÝ CHO GIAI ĐOẠN 1: XÁC NHẬN YÊU CẦU ĐỔI EMAIL (OTP TỪ EMAIL CŨ) ---
        if (model.Purpose == "ConfirmEmailChangeRequest_FromOldEmail")
        {
            _logger.LogInformation("VerifyOtpForAction POST: Processing 'ConfirmEmailChangeRequest_FromOldEmail' for CID {CID}", currentUserId);

            var pendingNewEmailFromTemp = TempData["PendingNewEmail_AfterOldEmailConfirmation"] as string;
            var customerIdForOldOtpFromTemp = TempData["ConfirmChangeEmail_CustomerId_ForOldOtp"] as string;

            // Giữ lại các TempData này nếu OTP sai để người dùng có thể thử lại
            TempData.Keep("PendingNewEmail_AfterOldEmailConfirmation");
            TempData.Keep("ConfirmChangeEmail_CustomerId_ForOldOtp");

            if (string.IsNullOrEmpty(pendingNewEmailFromTemp) ||
                !int.TryParse(customerIdForOldOtpFromTemp, out int customerIdStep1) ||
                customerIdStep1 != currentUserId)
            {
                ModelState.AddModelError(string.Empty, "Phiên làm việc không hợp lệ hoặc đã hết hạn. Vui lòng bắt đầu lại yêu cầu đổi email.");
                _logger.LogWarning("VerifyOtpForAction POST (Step1-Confirm): Invalid session data from TempData. PNE='{PNE}', CID_OldOtp='{CID_OldOtp}'", pendingNewEmailFromTemp, customerIdForOldOtpFromTemp);
                RemoveTempDataForStep1EmailChange(true);
                return RedirectToAction(nameof(RequestChangeEmail));
            }

            var otpTypeNameOld = "CustomerConfirmEmailChangeRequest";
            // Chuẩn bị expectedStatusOld đã là chữ thường
            string expectedStatusOld = $"ConfirmChangeReqTo:{pendingNewEmailFromTemp.ToLower()}";

            var otpRequestOld = await _context.OtpRequests
                .Include(o => o.OtpType) // Bao gồm OtpType để truy cập OtpTypeName
                .FirstOrDefaultAsync(o => o.CustomerID == currentUserId &&
                                         o.OtpCode == model.OtpCode &&
                                         o.OtpType.OtpTypeName == otpTypeNameOld && // So sánh trực tiếp OtpTypeName
                                         !o.IsUsed &&
                                         o.Status != null &&
                                         o.Status == expectedStatusOld && // So sánh trực tiếp Status (đã chuẩn hóa expectedStatusOld)
                                         o.ExpirationTime > DateTime.UtcNow);

            if (otpRequestOld == null)
            {
                ModelState.AddModelError(nameof(model.OtpCode), "Mã OTP xác nhận yêu cầu không hợp lệ, đã hết hạn hoặc sai.");
                _logger.LogWarning("VerifyOtpForAction POST (Step1-Confirm): Invalid OTP '{Otp}' for CID {CID}. Expected status: '{ExpectedStatus}'.", model.OtpCode, currentUserId, expectedStatusOld);
                return View("VerifyOtpGeneral", model);
            }

            _logger.LogInformation("VerifyOtpForAction POST (Step1-Confirm): OTP for OLD email VALID. CID {CID}. Proceeding to Step 2 (Verify New Email).", currentUserId);

            otpRequestOld.IsUsed = true;
            otpRequestOld.Status = $"OldEmailConfirmed_NewEmailPending:{pendingNewEmailFromTemp.Substring(0, Math.Min(pendingNewEmailFromTemp.Length, 10))}";

            var otpTypeNameNew = "CustomerChangeEmailVerification";
            var otpTypeNew = await _context.OtpTypes.FirstOrDefaultAsync(ot => ot.OtpTypeName == otpTypeNameNew);
            if (otpTypeNew == null)
            {
                _logger.LogCritical("CRITICAL: OtpType '{OtpT_New}' not found for new email verification.", otpTypeNameNew);
                ModelState.AddModelError(string.Empty, "Lỗi hệ thống (OTP-NV). Không thể tiếp tục.");
                await _context.SaveChangesAsync();
                RemoveTempDataForStep1EmailChange(true);
                return RedirectToAction(nameof(RequestChangeEmail));
            }

            var existingVerifyOtps = await _context.OtpRequests
                .Where(o => o.CustomerID == currentUserId && o.OtpTypeID == otpTypeNew.OtpTypeID && !o.IsUsed && o.ExpirationTime > DateTime.UtcNow)
                .ToListAsync();
            existingVerifyOtps.ForEach(o => { o.IsUsed = true; o.Status = $"InvalidatedByNewVerifyReq_For:{pendingNewEmailFromTemp.Substring(0, Math.Min(pendingNewEmailFromTemp.Length, 10))}"; });

            string otpCodeForNewEmail = GenerateOtp();
            var otpRequestNew = new OtpRequest
            {
                CustomerID = currentUserId,
                OtpTypeID = otpTypeNew.OtpTypeID,
                OtpCode = otpCodeForNewEmail,
                RequestTime = DateTime.UtcNow,
                ExpirationTime = DateTime.UtcNow.AddMinutes(15),
                IsUsed = false,
                Status = $"VerifyNewEmailAddr:{pendingNewEmailFromTemp.ToLower()}"
            };
            _context.OtpRequests.Add(otpRequestNew);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "DbUpdateException while saving OTP for new email verification. CID {CID}", currentUserId);
                if (ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx && sqlEx.Number == 2628)
                {
                    ModelState.AddModelError(string.Empty, "Lỗi hệ thống: Không thể lưu thông tin OTP (Status quá dài).");
                    _logger.LogError("TRUNCATION ERROR for OtpRequest.Status on new OTP. Value: {StatusValue}", otpRequestNew.Status);
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Lỗi hệ thống khi tạo OTP cho email mới.");
                }
                RemoveTempDataForStep1EmailChange(true);
                return RedirectToAction(nameof(RequestChangeEmail));
            }

            string emailSubjectNew = "MiniMart - Xác minh Địa chỉ Email Mới";
            string emailMessageNew = $"<p>Xin chào,</p><p>Yêu cầu thay đổi email của bạn đã được xác nhận từ địa chỉ email cũ.</p><p>Để hoàn tất việc thay đổi và xác minh địa chỉ email mới này (<strong>{pendingNewEmailFromTemp}</strong>) cho tài khoản MiniMart của bạn, vui lòng sử dụng mã OTP sau:</p><p style='font-size: 1.5em; font-weight: bold; text-align: center;'>{otpCodeForNewEmail}</p><p>Mã này sẽ hết hạn sau 15 phút.</p><p>Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email này.</p><p>Trân trọng,<br/>Đội ngũ MiniMart</p>";
            await _emailSender.SendEmailAsync(pendingNewEmailFromTemp, emailSubjectNew, emailMessageNew);
            _logger.LogInformation("VerifyOtpForAction POST (Step1-Confirm): OTP for NEW email verification sent to {NewEmail} for CID {CID}", pendingNewEmailFromTemp, currentUserId);

            RemoveTempDataForStep1EmailChange(false);

            TempData["OtpVerification_EmailToDisplay"] = pendingNewEmailFromTemp;
            TempData["OtpVerification_Purpose"] = "VerifyNewEmailAddress";
            string detailMessageForNewOtp = $"Một mã OTP mới đã được gửi đến <strong>{pendingNewEmailFromTemp}</strong> để xác minh. Vui lòng kiểm tra hộp thư (cả Spam/Junk) và nhập mã.";
            TempData["OtpVerification_DetailMessage"] = detailMessageForNewOtp;
            TempData["PendingVerifyNewEmail_ActualNewEmail"] = pendingNewEmailFromTemp;
            TempData["PendingVerifyNewEmail_CustomerId_ForNewOtp"] = currentUserId.ToString();

            _logger.LogInformation("VerifyOtpForAction POST (Step1-Confirm): Redirecting to VerifyOtpForAction for Step 2. New Purpose: '{NewPurpose}', EmailToDisplay: '{NewEmail}'", TempData["OtpVerification_Purpose"], TempData["OtpVerification_EmailToDisplay"]);
            return RedirectToAction(nameof(VerifyOtpForAction));
        }
        // --- XỬ LÝ CHO GIAI ĐOẠN 2: XÁC MINH EMAIL MỚI (OTP TỪ EMAIL MỚI) ---
        else if (model.Purpose == "VerifyNewEmailAddress")
        {
            _logger.LogInformation("VerifyOtpForAction POST: Processing 'VerifyNewEmailAddress' for CID {CID}", currentUserId);

            var newEmailToVerifyFromTemp = TempData["PendingVerifyNewEmail_ActualNewEmail"] as string;
            var customerIdForNewOtpFromTemp = TempData["PendingVerifyNewEmail_CustomerId_ForNewOtp"] as string;

            TempData.Keep("PendingVerifyNewEmail_ActualNewEmail");
            TempData.Keep("PendingVerifyNewEmail_CustomerId_ForNewOtp");

            if (string.IsNullOrEmpty(newEmailToVerifyFromTemp) ||
                !int.TryParse(customerIdForNewOtpFromTemp, out int customerIdStep2) ||
                customerIdStep2 != currentUserId)
            {
                ModelState.AddModelError(string.Empty, "Phiên xác minh email mới không hợp lệ hoặc đã hết hạn. Vui lòng bắt đầu lại yêu cầu đổi email.");
                _logger.LogWarning("VerifyOtpForAction POST (Step2-VerifyNew): Invalid session data from TempData. NewEmailToVerify='{NewEmail}', CID_NewOtp='{CID_NewOtp}'", newEmailToVerifyFromTemp, customerIdForNewOtpFromTemp);
                RemoveTempDataForStep2EmailChange();
                return RedirectToAction(nameof(RequestChangeEmail));
            }

            var otpTypeNameNew = "CustomerChangeEmailVerification";
            // Chuẩn bị expectedStatusNew đã là chữ thường
            string expectedStatusNew = $"VerifyNewEmailAddr:{newEmailToVerifyFromTemp.ToLower()}";

            var otpRequestNew = await _context.OtpRequests
                .Include(o => o.OtpType) // Bao gồm OtpType để truy cập OtpTypeName
                .FirstOrDefaultAsync(o => o.CustomerID == currentUserId &&
                                         o.OtpCode == model.OtpCode &&
                                         o.OtpType.OtpTypeName == otpTypeNameNew && // So sánh trực tiếp OtpTypeName
                                         !o.IsUsed &&
                                         o.Status != null &&
                                         o.Status == expectedStatusNew && // So sánh trực tiếp Status (đã chuẩn hóa expectedStatusNew)
                                         o.ExpirationTime > DateTime.UtcNow);

            if (otpRequestNew == null)
            {
                ModelState.AddModelError(nameof(model.OtpCode), "Mã OTP xác minh email mới không hợp lệ, đã hết hạn hoặc sai.");
                _logger.LogWarning("VerifyOtpForAction POST (Step2-VerifyNew): Invalid OTP '{Otp}' for CID {CID}. Expected status: '{ExpectedStatus}'.", model.OtpCode, currentUserId, expectedStatusNew);
                return View("VerifyOtpGeneral", model);
            }

            _logger.LogInformation("VerifyOtpForAction POST (Step2-VerifyNew): OTP for NEW email VALID. CID {CID}. Updating customer's email.", currentUserId);

            var customerToUpdate = await _context.Customers.FindAsync(currentUserId);
            if (customerToUpdate == null)
            {
                _logger.LogError("CRITICAL: Customer {CID} not found during final email update step.", currentUserId);
                TempData["ErrorMessage_Profile"] = "Lỗi nghiêm trọng: Tài khoản không tồn tại khi cập nhật email.";
                otpRequestNew.IsUsed = true;
                otpRequestNew.Status = "ErrorUserNotFoundOnFinal";
                await _context.SaveChangesAsync();
                RemoveTempDataForStep2EmailChange();
                return RedirectToAction(nameof(Settings));
            }

            if (await _context.Customers.AnyAsync(c => c.Email.ToLower() == newEmailToVerifyFromTemp.ToLower() && c.CustomerID != currentUserId))
            {
                _logger.LogWarning("VerifyOtpForAction POST (Step2-VerifyNew): New email {NewEmail} was taken by another account during OTP verification. CID {CID}.", newEmailToVerifyFromTemp, currentUserId);
                TempData["ErrorMessage_Profile"] = $"Không thể cập nhật. Email '{newEmailToVerifyFromTemp}' đã bị tài khoản khác sử dụng. Vui lòng thử lại.";
                otpRequestNew.IsUsed = true;
                otpRequestNew.Status = "ConflictOnNewEmailFinal";
                await _context.SaveChangesAsync();
                RemoveTempDataForStep2EmailChange();
                return RedirectToAction(nameof(RequestChangeEmail));
            }

            var oldEmailInDb = customerToUpdate.Email;
            customerToUpdate.Email = newEmailToVerifyFromTemp;
            customerToUpdate.IsEmailVerified = true;
            customerToUpdate.EmailVerifiedAt = DateTime.UtcNow;
            otpRequestNew.IsUsed = true;
            otpRequestNew.Status = "NewEmailFinalizedAndVerified";

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("VerifyOtpForAction POST (Step2-VerifyNew): Customer email updated successfully for CID {CID} to {NewEmail}", currentUserId, newEmailToVerifyFromTemp);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "DbUpdateException while finalizing email change for CID {CID}", currentUserId);
                TempData["ErrorMessage_Profile"] = "Lỗi hệ thống khi cập nhật email. Vui lòng thử lại.";
                RemoveTempDataForStep2EmailChange();
                return RedirectToAction(nameof(Settings));
            }

            if (!string.IsNullOrEmpty(oldEmailInDb) && !oldEmailInDb.Equals(newEmailToVerifyFromTemp, StringComparison.OrdinalIgnoreCase))
            {
                string notifyOldEmailSubject = "MiniMart - Thông báo: Địa chỉ Email Tài khoản Đã Thay Đổi";
                string notifyOldEmailMessage = $"<p>Xin chào,</p><p>Địa chỉ email liên kết với tài khoản MiniMart của bạn (Tên đăng nhập: {customerToUpdate.Username}) đã được thay đổi thành công từ <strong>{oldEmailInDb}</strong> thành <strong>{newEmailToVerifyFromTemp}</strong>.</p><p>Nếu bạn không thực hiện thay đổi này, vui lòng liên hệ với bộ phận hỗ trợ của chúng tôi ngay lập tức.</p><p>Trân trọng,<br/>Đội ngũ MiniMart</p>";
                await _emailSender.SendEmailAsync(oldEmailInDb, notifyOldEmailSubject, notifyOldEmailMessage);
                _logger.LogInformation("Notification of email change sent to OLD email {OldEmail} for CID {CID}", oldEmailInDb, currentUserId);
            }

            TempData["SuccessMessage_Profile"] = $"Địa chỉ email của bạn đã được thay đổi và xác minh thành công thành <strong>{newEmailToVerifyFromTemp}</strong>!";
            RemoveTempDataForStep2EmailChange();
            return RedirectToAction(nameof(Settings));
        }
        // ... (Thêm các else if cho các purpose khác như ChangePassword, InitialEmailVerification, LoginTwoFactorVerification)
        else
        {
            _logger.LogWarning("VerifyOtpForAction POST: Unknown or unhandled purpose '{Purpose}'.", model.Purpose);
            ModelState.AddModelError(string.Empty, "Mục đích xác thực không hợp lệ hoặc không được hỗ trợ.");
        }

        return View("VerifyOtpGeneral", model);
    }


    // --- HÀM HELPER ĐỂ QUẢN LÝ TempData ---
    private void KeepSpecificTempDataForPurpose(string? purpose)
    {
        if (string.IsNullOrEmpty(purpose)) return;

        _logger.LogDebug("KeepSpecificTempDataForPurpose called for: '{Purpose}'", purpose);

        // Các key chung đã được TempData.Keep() ở action GET rồi, ở đây chỉ keep các key rất đặc thù
        // mà có thể cần cho lần POST tiếp theo NẾU action GET không được gọi lại (ít xảy ra với redirect).
        // Chủ yếu hữu ích nếu bạn return View() từ action POST và muốn giữ lại state cho lần POST sau.

        if (purpose == "ConfirmEmailChangeRequest_FromOldEmail")
        {
            TempData.Keep("PendingNewEmail_AfterOldEmailConfirmation");
            TempData.Keep("ConfirmChangeEmail_CustomerId_ForOldOtp");
        }
        else if (purpose == "VerifyNewEmailAddress")
        {
            TempData.Keep("PendingVerifyNewEmail_ActualNewEmail");
            TempData.Keep("PendingVerifyNewEmail_CustomerId_ForNewOtp");
        }
        else if (purpose == "InitialEmailVerification" || purpose == "CustomerAccountVerification")
        {
            TempData.Keep("VerificationEmail"); // Hoặc key bạn dùng cho email đang xác minh
            TempData.Keep("CustomerIdForVerification"); // Nếu bạn lưu CustomerId
        }
        // ... Thêm các case khác nếu cần
    }

    private string ModelStateErrorsToString(ModelStateDictionary modelState)
    {
        return string.Join("; ", modelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
    }

    private void RemoveTempDataForStep1EmailChange(bool clearOtpVerificationGlobals)
    {
        _logger.LogDebug("Removing TempData for Step 1 Email Change. ClearOtpGlobals: {ClearGlobals}", clearOtpVerificationGlobals);
        TempData.Remove("PendingNewEmail_AfterOldEmailConfirmation");
        TempData.Remove("ConfirmChangeEmail_CustomerId_ForOldOtp");
        if (clearOtpVerificationGlobals)
        {
            TempData.Remove("OtpVerification_EmailToDisplay");
            TempData.Remove("OtpVerification_Purpose");
            TempData.Remove("OtpVerification_DetailMessage");
            TempData.Remove("LastVerificationDetail");
        }
    }

    private void RemoveTempDataForStep2EmailChange()
    {
        _logger.LogDebug("Removing TempData for Step 2 Email Change (Final).");
        TempData.Remove("OtpVerification_EmailToDisplay");
        TempData.Remove("OtpVerification_Purpose");
        TempData.Remove("OtpVerification_DetailMessage");
        TempData.Remove("PendingVerifyNewEmail_ActualNewEmail");
        TempData.Remove("PendingVerifyNewEmail_CustomerId_ForNewOtp");
        TempData.Remove("LastVerificationDetail");
    }

    // Placeholder cho Settings Action để redirect đến


}