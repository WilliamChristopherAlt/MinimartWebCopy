// Trong Models/VerifyOtpViewModel.cs (hoặc ViewModels/)
using System.ComponentModel.DataAnnotations;

namespace MinimartWeb.Models
{
    public class VerifyOtpViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập địa chỉ email.")]
        [EmailAddress]
        public string Email { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã OTP.")]
        [StringLength(6, ErrorMessage = "Mã OTP phải có 6 chữ số.")]
        [Display(Name = "Mã OTP")]
        public string OtpCode { get; set; }

    }
}