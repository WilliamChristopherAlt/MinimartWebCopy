using System.ComponentModel.DataAnnotations;

namespace MinimartWeb.Model
{
    public class Supplier
    {
        [Key]
        public int SupplierID { get; set; }

        [Required]
        [StringLength(255)]
        [Display(Name = "Tên nhà cung cấp")]
        public string SupplierName { get; set; }

        [Required]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Số điện thoại phải đúng 10 chữ số.")]
        [Display(Name = "Số điện thoại")]
        public string SupplierPhoneNumber { get; set; }

        [Required]
        [StringLength(255)]
        [Display(Name = "Địa chỉ")]
        public string SupplierAddress { get; set; }

        [Required]
        [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ.")]
        [StringLength(255)]
        [Display(Name = "Email")]
        public string SupplierEmail { get; set; }

        // Navigation property
        public ICollection<ProductType> ProductTypes { get; set; } = new List<ProductType>();
    }
}
