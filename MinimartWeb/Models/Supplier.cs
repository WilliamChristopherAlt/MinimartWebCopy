using System.ComponentModel.DataAnnotations;

namespace MinimartWeb.Model
{
    public class Supplier
    {
        [Key]
        public int SupplierID { get; set; }

        [Required]
        [StringLength(255)]
        [Display(Name = "Name")]
        public string SupplierName { get; set; }

        [Required]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Phone number must be exactly 10 digits.")]
        [Display(Name = "Phone Number")]
        public string SupplierPhoneNumber { get; set; }

        [Required]
        [StringLength(255)]
        [Display(Name = "Address")]
        public string SupplierAddress { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(255)]
        [Display(Name = "Email")]
        public string SupplierEmail { get; set; }

        // Navigation property
        public ICollection<ProductType> ProductTypes { get; set; } = new List<ProductType>();
    }
}
