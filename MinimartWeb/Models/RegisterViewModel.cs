// Trong file: Models/RegisterViewModel.cs (hoặc ViewModels/RegisterViewModel.cs)
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http; // Để sử dụng IFormFile cho việc tải ảnh lên

namespace MinimartWeb.Models // Hoặc namespace ViewModels của bạn
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Họ là bắt buộc.")]
        [StringLength(50, ErrorMessage = "Họ không được vượt quá 50 ký tự.")]
        [Display(Name = "Họ")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Tên là bắt buộc.")]
        [StringLength(50, ErrorMessage = "Tên không được vượt quá 50 ký tự.")]
        [Display(Name = "Tên")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "Email là bắt buộc.")]
        [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ.")]
        [StringLength(255, ErrorMessage = "Email không được vượt quá 255 ký tự.")]
        [Display(Name = "Địa chỉ Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Số điện thoại là bắt buộc.")]
        [RegularExpression(@"^(0[1-9][0-9]{8})$", ErrorMessage = "Số điện thoại phải có 10 chữ số và bắt đầu bằng số 0 hợp lệ.")]
        [Display(Name = "Số điện thoại")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Tên đăng nhập là bắt buộc.")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Tên đăng nhập phải có từ 3 đến 50 ký tự.")]
        [RegularExpression(@"^[a-zA-Z0-9_.]+$", ErrorMessage = "Tên đăng nhập chỉ được chứa chữ cái, số, dấu gạch dưới (_) và dấu chấm (.).")]
        [Display(Name = "Tên đăng nhập")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Mật khẩu là bắt buộc.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự.")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu.")]
        [DataType(DataType.Password)]
        [Display(Name = "Xác nhận mật khẩu")]
        [Compare("Password", ErrorMessage = "Mật khẩu và mật khẩu xác nhận không khớp.")]
        public string ConfirmPassword { get; set; }

        // Cho phép người dùng tải ảnh đại diện khi đăng ký (tùy chọn)
        // Nếu không muốn có chức năng này ngay, bạn có thể comment hoặc bỏ qua trường này
        [Display(Name = "Ảnh đại diện (Tùy chọn)")]
        public IFormFile? ProfileImage { get; set; } // IFormFile cho phép tải file lên

        // Bạn có thể thêm các trường khác nếu cần, ví dụ:
        // [Range(typeof(bool), "true", "true", ErrorMessage = "Bạn phải đồng ý với điều khoản dịch vụ.")]
        // [Display(Name = "Tôi đồng ý với các điều khoản dịch vụ")]
        // public bool AcceptTerms { get; set; }
    }
}