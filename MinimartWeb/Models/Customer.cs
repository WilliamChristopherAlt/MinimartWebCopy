// Trong file: Model/Customer.cs
using MinimartWeb.Models;
using System;
using System.Collections.Generic; // Cần cho ICollection
using System.ComponentModel.DataAnnotations;

namespace MinimartWeb.Model
{
    public class Customer
    {
        [Key]
        public int CustomerID { get; set; }

        [Required(ErrorMessage = "Họ là bắt buộc.")]
        [StringLength(255)]
        [Display(Name = "Họ")]
        public string FirstName { get; set; }

        // ... (các thuộc tính khác của Customer giữ nguyên) ...
        [Required]
        [StringLength(255)]
        [Display(Name = "Tên")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "Email là bắt buộc.")]
        [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ.")]
        [StringLength(255)]
        [Display(Name = "Địa chỉ Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Số điện thoại là bắt buộc.")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Số điện thoại phải có đúng 10 chữ số.")]
        [Display(Name = "Số điện thoại")]
        public string PhoneNumber { get; set; }

        [StringLength(512)]
        [Display(Name = "Ảnh đại diện")]
        public string? ImagePath { get; set; }

        [Required(ErrorMessage = "Tên đăng nhập là bắt buộc.")]
        [StringLength(255)]
        [Display(Name = "Tên đăng nhập")]
        public string Username { get; set; }

        [Required]
        public byte[] PasswordHash { get; set; }

        [Required]
        public byte[] Salt { get; set; }

        [Display(Name = "Đã xác minh Email")]
        public bool IsEmailVerified { get; set; }

        [Display(Name = "Thời điểm xác minh Email")]
        public DateTime? EmailVerifiedAt { get; set; }
        [Required] // Vì trong DB bạn set NOT NULL DEFAULT 0
        public bool Is2FAEnabled { get; set; }
        // Thuộc tính này sẽ được EF map với cột Is2FAEnabled trong bảng Customers


        // === NAVIGATION PROPERTIES CẦN THÊM/SỬA ===
        public virtual ICollection<Sale> Sales { get; set; } = new HashSet<Sale>(); // Collection các Sale liên quan
        public virtual ICollection<OtpRequest> OtpRequests { get; set; } = new HashSet<OtpRequest>(); // Collection các OtpRequest liên quan
        public virtual ICollection<ViewHistory> ViewHistories { get; set; } = new HashSet<ViewHistory>(); // Nếu có
        public virtual ICollection<SearchHistory> SearchHistories { get; set; } = new HashSet<SearchHistory>(); // Nếu có
        public virtual ICollection<Notification> Notifications { get; set; }
        public virtual ICollection<LoginAttempt> LoginAttempts { get; set; }
    }
}