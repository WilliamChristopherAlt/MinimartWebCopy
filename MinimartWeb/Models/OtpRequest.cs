using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MinimartWeb.Model
{
    public class OtpRequest 
    {
        [Key]
        public int OtpRequestID { get; set; }

        [Display(Name = "Customer")]
        public int? CustomerID { get; set; }
        [Display(Name = "Employee Account")]
        public int? EmployeeAccountID { get; set; }

        [Required]
        [Display(Name = "OTP Type")]
        public int OtpTypeID { get; set; }

        [Required]
        [StringLength(6)]
        [Display(Name = "OTP Code")]
        public string OtpCode { get; set; }

        [Required]
        [Display(Name = "Request Time")]
        public DateTime RequestTime { get; set; } = DateTime.Now;

        [Required]
        [Display(Name = "Expiration Time")]
        public DateTime ExpirationTime { get; set; }

        [Required]
        [Display(Name = "Is Used")]
        public bool IsUsed { get; set; } = false;

        [StringLength(50)]
        [Display(Name = "Status")]
        public string Status { get; set; }

        // Navigation properties
        public Customer Customer { get; set; }
        public EmployeeAccount EmployeeAccount { get; set; }
        public OtpType OtpType { get; set; }
    }
}
