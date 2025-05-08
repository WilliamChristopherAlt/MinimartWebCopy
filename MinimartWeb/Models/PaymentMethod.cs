using System.ComponentModel.DataAnnotations;

namespace MinimartWeb.Model
{
    public class PaymentMethod
    {
        [Key]
        public int PaymentMethodID { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Method Name")]
        public string MethodName { get; set; }

        // Navigation property
        public ICollection<Sale> Sales { get; set; } = new HashSet<Sale>();
    }
}
