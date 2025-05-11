// Trong file: ViewModels/CustomerProfileViewModel.cs
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace MinimartWeb.Models
{
    public class CustomerProfileViewModel
    {
        public int CustomerId { get; set; }

        [Required(ErrorMessage = "Họ không được để trống.")]
        [StringLength(255)]
        [Display(Name = "Họ")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên không được để trống.")]
        [StringLength(255)]
        [Display(Name = "Tên")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email không được để trống.")]
        [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ.")]
        [StringLength(255)]
        [Display(Name = "Địa chỉ Email")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Đã xác minh Email")]
        public bool IsEmailVerified { get; set; } // Giữ lại cái này

        // BỎ DÒNG NÀY VÌ KHÔNG CÓ TRONG BẢNG CUSTOMERS
        // [Display(Name = "Thời điểm xác minh Email")]
        // public DateTime? EmailVerifiedAt { get; set; }

        [Required(ErrorMessage = "Số điện thoại không được để trống.")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Số điện thoại phải có đúng 10 chữ số.")]
        [Display(Name = "Số điện thoại")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Display(Name = "Ảnh đại diện hiện tại")]
        public string? ImagePath { get; set; }

        [Display(Name = "Tải ảnh đại diện mới")]
        public IFormFile? NewImageFile { get; set; }

        [Display(Name = "Tên đăng nhập")]
        public string Username { get; set; } = string.Empty;
    }
}