using System.ComponentModel.DataAnnotations;

namespace MinimartWeb.Models
{
    public class ResetPasswordViewModel
    {
        [Required]
        public string Email { get; set; } // Input ẩn, hoặc lấy từ token/TempData

        [Required]
        public string UserType { get; set; } // Input ẩn, hoặc lấy từ token/TempData

        [Required(ErrorMessage = "Vui lòng nhập mã OTP.")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP phải có 6 chữ số.")]
        [Display(Name = "Mã OTP")]
        public string OtpCode { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự.")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu mới")]
        public string NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Xác nhận mật khẩu mới")]
        [Compare("NewPassword", ErrorMessage = "Mật khẩu mới và mật khẩu xác nhận không khớp.")]
        public string ConfirmNewPassword { get; set; }
    }
}
