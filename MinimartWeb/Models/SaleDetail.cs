using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MinimartWeb.Model
{
    public class SaleDetail
    {
        [Key]
        public int SaleDetailID { get; set; }

        [Required]
        [Display(Name = "Sale ID")]
        public int SaleID { get; set; }

        [Required]
        [Display(Name = "Product Type")]
        public int ProductTypeID { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Quantity must be greater than zero.")]
        [Display(Name = "Quantity")]
        public decimal Quantity { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        [Range(0, double.MaxValue)]
        [Display(Name = "Product Price At Purchase")]
        public decimal ProductPriceAtPurchase { get; set; }

        // Navigation properties
        public Sale Sale { get; set; }
        public ProductType ProductType { get; set; }
    }
}
