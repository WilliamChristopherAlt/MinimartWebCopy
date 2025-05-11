// Trong ViewModels/ForgotPasswordViewModel.cs (hoặc Models/)
using System.ComponentModel.DataAnnotations;

namespace MinimartWeb.Models; // Hoặc MinimartWeb.Models

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập địa chỉ email của bạn.")]
    [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ.")]
    [Display(Name = "Địa chỉ Email đã đăng ký")]
    public string Email { get; set; }

    // Thêm trường này để người dùng chọn nếu họ quên mật khẩu cho tài khoản Customer hay Employee
    [Required(ErrorMessage = "Vui lòng chọn loại tài khoản.")]
    [Display(Name = "Bạn muốn đặt lại mật khẩu cho tài khoản")]
    public string UserType { get; set; } // Giá trị sẽ là "Customer" hoặc "Employee"
}