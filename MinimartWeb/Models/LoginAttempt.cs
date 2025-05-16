using MinimartWeb.Model;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace MinimartWeb.Models
{
    public class LoginAttempt
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public DateTime AttemptTime { get; set; }

        [Required]
        public bool IsSuccessful { get; set; }

        [Required]
        [MaxLength(45)]
        public string IPAddress { get; set; }

        // Foreign Keys (Mutually Exclusive)
        [ForeignKey("Customer")]
        public int? CustomerID { get; set; }

        [ForeignKey("EmployeeAccount")]
        public int? EmployeeAccountID { get; set; }

        // Navigation properties
        public virtual Customer Customer { get; set; }
        public virtual EmployeeAccount EmployeeAccount { get; set; }

        // 🛡️ Ensure Mutual Exclusivity
        [NotMapped]
        public bool IsValid =>
            (CustomerID.HasValue && !EmployeeAccountID.HasValue) ||
            (!CustomerID.HasValue && EmployeeAccountID.HasValue);
    }
}
