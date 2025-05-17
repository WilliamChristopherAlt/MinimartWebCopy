// Trong file: Model/EmployeeAccount.cs
using System;
using System.Collections.Generic; // Cần cho ICollection
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MinimartWeb.Model
{
    public class EmployeeAccount
    {
        [Key]
        public int AccountID { get; set; }

        [Required]
        [Display(Name = "Employee ID")]
        public int EmployeeID { get; set; }

        [Required]
        [StringLength(255)]
        [Display(Name = "Username")]
        public string Username { get; set; }

        [Required]
        [MaxLength(64)]
        [Display(Name = "Password Hash")]
        public byte[] PasswordHash { get; set; }

        [Required]
        [MaxLength(64)]
        [Display(Name = "Salt")]
        public byte[] Salt { get; set; }

        [Display(Name = "Created At")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "Last Login")]
        public DateTime? LastLogin { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Admin Role")]
        public bool IsAdmin { get; set; } = false;

        [Display(Name = "Đã xác minh Email")]
        public bool IsEmailVerified { get; set; } // Giả sử bạn đã thêm cột này vào DB

        [Display(Name = "Thời điểm xác minh Email")]
        public DateTime? EmailVerifiedAt { get; set; } // Giả sử bạn đã thêm cột này vào DB

        [Required]
        public bool Is2FAEnabled { get; set; }

        // === NAVIGATION PROPERTIES CẦN THÊM/SỬA ===
        [ForeignKey("EmployeeID")]
        public virtual Employee? Employee { get; set; } // Thuộc tính navigation đến Employee
                                                        // Thêm virtual để cho phép lazy loading
                                                        // Thêm ? để cho phép Employee là null nếu EmployeeID là nullable (mặc dù trong trường hợp này EmployeeID là Required)
                                                        // Tuy nhiên, vì EmployeeID là NOT NULL, Employee không nên là nullable trừ khi có lý do đặc biệt


        public virtual ICollection<OtpRequest> OtpRequests { get; set; } = new HashSet<OtpRequest>(); // Collection các OtpRequest liên quan

        [Required] // Vì trong DB bạn set NOT NULL DEFAULT 0
        public bool Is2FAEnabled { get; set; }
        // Thuộc tính này sẽ được EF map với cột Is2FAEnabled trong bảng EmployeeAccounts
    }
}