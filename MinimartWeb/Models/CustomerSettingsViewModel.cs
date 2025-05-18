// File: ViewModels/CustomerSettingsViewModel.cs
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace MinimartWeb.Models // HOẶC MinimartWeb.Models TÙY CẤU TRÚC CỦA BẠN
{
    public class CustomerSettingsViewModel
    {
        public int CustomerId { get; set; }

        [Required(ErrorMessage = "Họ không được để trống.")]
        [StringLength(255, ErrorMessage = "Họ không được vượt quá 255 ký tự.")]
        [Display(Name = "Họ")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên không được để trống.")]
        [StringLength(255, ErrorMessage = "Tên không được vượt quá 255 ký tự.")]
        [Display(Name = "Tên")]
        public string LastName { get; set; } = string.Empty;

        [Display(Name = "Địa chỉ Email")]
        public string Email { get; set; } = string.Empty; // Chỉ hiển thị

        [Display(Name = "Tên đăng nhập")]
        public string Username { get; set; } = string.Empty; // Chỉ hiển thị

        [Required(ErrorMessage = "Số điện thoại không được để trống.")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Số điện thoại phải có đúng 10 chữ số.")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
        [Display(Name = "Số điện thoại")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Display(Name = "Ảnh đại diện hiện tại")]
        public string? ImagePath { get; set; }

        [Display(Name = "Tải ảnh đại diện mới")]
        public IFormFile? NewImageFile { get; set; } // Để trống nếu không đổi

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại để xác nhận.")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu hiện tại")]
        public string CurrentPassword { get; set; } = string.Empty;
        [Display(Name = "Xác thực hai yếu tố (2FA) qua Email")]
        public bool Is2FAEnabled { get; set; } // Trạng thái hiện tại

        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu hiện tại")]
        public string? PasswordForChange2FAStatus { get; set; } // Dùng khi bật hoặc tắt
    }
}