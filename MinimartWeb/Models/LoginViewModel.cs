// Trong Models/LoginViewModel.cs
using System.ComponentModel.DataAnnotations;

public class LoginViewModel
{
    [Required(ErrorMessage = "Tên đăng nhập là bắt buộc.")]
    public string Username { get; set; }

    [Required(ErrorMessage = "Mật khẩu là bắt buộc.")]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn loại người dùng.")]
    public string UserType { get; set; } // "Customer" hoặc "Employee"

    [Display(Name = "Ghi nhớ tôi?")]
    public bool RememberMe { get; set; } // Thêm thuộc tính này
}