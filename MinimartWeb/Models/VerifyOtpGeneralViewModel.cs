using System.ComponentModel.DataAnnotations;

namespace MinimartWeb.Models // HOẶC MinimartWeb.Models
{
    public class VerifyOtpGeneralViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập mã OTP.")]
        [StringLength(6, ErrorMessage = "Mã OTP phải có 6 chữ số.", MinimumLength = 6)]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã OTP chỉ được chứa chữ số.")]
        [Display(Name = "Mã OTP")]
        public string OtpCode { get; set; } = string.Empty;

        // Trường này giúp action biết OTP này dùng cho mục đích gì khi POST
        // Ví dụ: "ChangePassword", "ChangeEmail_NewEmailVerification", "InitialEmailVerification"
        public string Purpose { get; set; } = string.Empty;

        // Thông tin chi tiết để hiển thị trên trang xác thực OTP (ví dụ: email nào đã nhận OTP)
        public string? VerificationDetail { get; set; }
    }
}