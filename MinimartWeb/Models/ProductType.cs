using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MinimartWeb.Model
{
    public class ProductType
    {
        [Key]
        public int ProductTypeID { get; set; }

        [Required]
        [StringLength(255)]
        [Display(Name = "Product Name")]
        public string ProductName { get; set; }

        [Required]
        [Display(Name = "Product Description")]
        public string ProductDescription { get; set; }

        [Required]
        [Display(Name = "Category")]
        public int CategoryID { get; set; }

        [Required]
        [Display(Name = "Supplier")]
        public int SupplierID { get; set; }

        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Price must be a positive value.")]
        [Display(Name = "Price")]
        public decimal Price { get; set; }

        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Stock Amount must be a positive value.")]
        [Display(Name = "Stock Amount")]
        public decimal StockAmount { get; set; }

        [Required]
        [Display(Name = "Measurement Unit")]
        public int MeasurementUnitID { get; set; }

        [Display(Name = "Expiration Duration (Days)")]
        public int? ExpirationDurationDays { get; set; }

        [Required]
        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        [Required]
        [Display(Name = "Date Added")]
        public DateTime DateAdded { get; set; } = DateTime.Now;

        [Display(Name = "Image Path")]
        public string ImagePath { get; set; }

        // Navigation properties
        public Category Category { get; set; }
        public Supplier Supplier { get; set; }
        public MeasurementUnit MeasurementUnit { get; set; }
        public ICollection<ProductTag> ProductTags { get; set; } = new List<ProductTag>();
        public ICollection<SaleDetail> SaleDetails { get; set; } = new List<SaleDetail>();

        [NotMapped]
        public List<string> Tags => ProductTags?.Select(pt => pt.Tag.TagName).ToList() ?? new List<string>();

    }
}
