using System.ComponentModel.DataAnnotations;

namespace MinimartWeb.Models // HOẶC MinimartWeb.Models
{
    public class VerifyOtpGeneralViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập mã OTP.")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP phải có 6 chữ số.")]
        [RegularExpression("^[0-9]{6}$", ErrorMessage = "Mã OTP chỉ chứa chữ số.")]
        [Display(Name = "Mã OTP")]
        public string OtpCode { get; set; } = string.Empty;

        public string Purpose { get; set; } = string.Empty; // Sẽ được set từ Controller hoặc input ẩn
        public string? VerificationDetail { get; set; } // Thông báo chi tiết, được set từ Controller
        public string? Email { get; set; } // Email liên quan đến OTP (để hiển thị nếu cần)
    }
}