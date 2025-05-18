// File: ViewModels/EmployeeProfileViewModel.cs
using Microsoft.AspNetCore.Http;
using System;
using System.ComponentModel.DataAnnotations;

namespace MinimartWeb.Models // HOẶC MinimartWeb.Models
{
    public class EmployeeProfileViewModel
    {
        public int EmployeeId { get; set; } // Để biết là profile của ai

        [Display(Name = "Họ")]
        public string FirstName { get; set; } = string.Empty;

        [Display(Name = "Tên")]
        public string LastName { get; set; } = string.Empty;

        [Display(Name = "Email công ty")]
        public string Email { get; set; } = string.Empty; // Thường không cho nhân viên tự sửa

        [Display(Name = "Số điện thoại cá nhân")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Số điện thoại phải có đúng 10 chữ số.")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
        public string PhoneNumber { get; set; } = string.Empty; // Có thể cho sửa

        [Display(Name = "Giới tính")]
        public string Gender { get; set; } = string.Empty; // Chỉ hiển thị

        [Display(Name = "Ngày sinh")]
        [DataType(DataType.Date)]
        public DateTime BirthDate { get; set; } // Chỉ hiển thị

        [Display(Name = "Số CCCD/CMND")]
        public string CitizenID { get; set; } = string.Empty; // Chỉ hiển thị

        [Display(Name = "Ngày vào làm")]
        [DataType(DataType.Date)]
        public DateTime HireDate { get; set; } // Chỉ hiển thị

        [Display(Name = "Chức vụ")]
        public string RoleName { get; set; } = string.Empty; // Chỉ hiển thị

        [Display(Name = "Ảnh đại diện")]
        public string? ImagePath { get; set; }

        // Cho phép cập nhật ảnh
        [Display(Name = "Tải ảnh đại diện mới")]
        public IFormFile? NewImageFile { get; set; }

        // Nếu cho phép nhân viên tự sửa một số thông tin và cần xác thực
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu hiện tại (để xác nhận thay đổi)")]
        public string? CurrentPasswordForUpdate { get; set; } // Bắt buộc nếu form cho phép sửa

        // Thông tin từ EmployeeAccounts (nếu cần hiển thị)
        [Display(Name = "Tên đăng nhập")]
        public string Username { get; set; } = string.Empty; // Chỉ hiển thị

        [Display(Name = "Tài khoản hoạt động")]
        public bool IsAccountActive { get; set; } // Chỉ hiển thị

        [Display(Name = "Email Nhân viên Đã xác minh")]
        public bool IsEmployeeEmailVerified { get; set; }

        [Display(Name = "Thời điểm xác minh Email NV")]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm}")]
        public DateTime? EmployeeEmailVerifiedAt { get; set; }

        [Display(Name = "Xác thực hai yếu tố (2FA) qua Email")]
        public bool Is2FAEnabled { get; set; } // Trạng thái hiện tại

        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu hiện tại")]
        public string? PasswordForChange2FAStatus { get; set; } // Dùng khi bật hoặc tắt
    }
}