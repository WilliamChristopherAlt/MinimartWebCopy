
using System.ComponentModel.DataAnnotations;


namespace MinimartWeb.Models; // Hoặc MinimartWeb.Models tùy theo cấu trúc của bạn

public class VerifyLoginOtpViewModel
{
    // Các trường này sẽ là input ẩn trong form,
    // được điền từ TempData bởi action GET và gửi lại trong action POST.
    [Required]
    public string Username { get; set; }

    [Required]
    public string UserType { get; set; } // Giá trị sẽ là "Customer" hoặc "Employee"

    // Email này chủ yếu để hiển thị lại cho người dùng biết OTP đã gửi về đâu.
    // Nó không nhất thiết phải được submit lại nếu Username và UserType đã đủ để định danh.
    // Tuy nhiên, để đơn giản, chúng ta có thể submit lại.
    public string? EmailForDisplay { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã OTP.")]
    [StringLength(6, ErrorMessage = "Mã OTP phải có 6 chữ số.")]
    [RegularExpression("^[0-9]{6}$", ErrorMessage = "Mã OTP chỉ được chứa 6 chữ số.")]
    [Display(Name = "Mã OTP Xác Thực")]
    public string OtpCode { get; set; }
}