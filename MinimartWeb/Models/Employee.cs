using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MinimartWeb.Model
{
    public class Employee
    {
        [Key]
        public int EmployeeID { get; set; }

        [Required]
        [StringLength(255)]
        [Display(Name = "Họ")]
        public string FirstName { get; set; }

        [Required]
        [StringLength(255)]
        [Display(Name = "Tên")]
        public string LastName { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(255)]
        [Display(Name = "Địa chỉ Email")]
        public string Email { get; set; }

        [Required]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Số điện thoại phải có đúng 10 chữ số.")]
        [Display(Name = "Số điện thoại")]
        public string PhoneNumber { get; set; }

        [Required]
        [StringLength(20)]
        [RegularExpression(@"^(Nam|Nữ|Khác|)$", ErrorMessage = "Giới tính không hợp lệ.")]
        [Display(Name = "Giới tính")]
        public string Gender { get; set; }

        [Required]
        [Display(Name = "Ngày sinh")]
        public DateTime BirthDate { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "CMND/CCCD")]
        public string CitizenID { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        [Range(0, double.MaxValue)]
        [Display(Name = "Lương")]
        public decimal? Salary { get; set; }

        [Required]
        [Display(Name = "Ngày tuyển dụng")]
        public DateTime HireDate { get; set; } = DateTime.Now;

        [Required]
        [Display(Name = "Vai trò")]
        public int RoleID { get; set; }

        [StringLength(512)]
        [Display(Name = "Ảnh đại diện")]
        public string? ImagePath { get; set; }

        // Navigation properties
        public EmployeeRole Role { get; set; }
        public virtual EmployeeAccount? EmployeeAccount { get; set; }

        public ICollection<Sale> Sales { get; set; } = new HashSet<Sale>();
    }
}
