using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MinimartWeb.Model
{
    public class EmployeeAccount
    {
        [Key]
        public int AccountID { get; set; }

        [Required]
        [Display(Name = "Mã nhân viên")]
        public int EmployeeID { get; set; }

        [Required]
        [StringLength(255)]
        [Display(Name = "Tên đăng nhập")]
        public string Username { get; set; }

        [Required]
        [MaxLength(64)]
        [Display(Name = "Mã hóa mật khẩu")]
        public byte[] PasswordHash { get; set; }

        [Required]
        [MaxLength(64)]
        [Display(Name = "Chuỗi Salt")]
        public byte[] Salt { get; set; }

        [Display(Name = "Ngày tạo")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "Lần đăng nhập gần nhất")]
        public DateTime? LastLogin { get; set; }

        [Display(Name = "Đang hoạt động")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Quyền quản trị")]
        public bool IsAdmin { get; set; } = false;

        [Display(Name = "Đã xác minh Email")]
        public bool IsEmailVerified { get; set; }

        [Display(Name = "Thời điểm xác minh Email")]
        public DateTime? EmailVerifiedAt { get; set; }

        [Required]
        [Display(Name = "Đã bật xác thực 2 bước")]
        public bool Is2FAEnabled { get; set; }

        // Navigation properties
        [ForeignKey("EmployeeID")]
        public virtual Employee? Employee { get; set; }

        public virtual ICollection<OtpRequest> OtpRequests { get; set; } = new HashSet<OtpRequest>();
    }
}
