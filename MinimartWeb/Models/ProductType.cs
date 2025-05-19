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
        [Display(Name = "Tên sản phẩm")]
        public string ProductName { get; set; }

        [Required]
        [Display(Name = "Mô tả sản phẩm")]
        public string ProductDescription { get; set; }

        [Required]
        [Display(Name = "Danh mục")]
        public int CategoryID { get; set; }

        [Required]
        [Display(Name = "Nhà cung cấp")]
        public int SupplierID { get; set; }

        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Giá phải là một số dương.")]
        [Display(Name = "Giá")]
        public decimal Price { get; set; }

        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Số lượng tồn kho phải là số dương.")]
        [Display(Name = "Số lượng tồn kho")]
        public decimal StockAmount { get; set; }

        [Required]
        [Display(Name = "Đơn vị tính")]
        public int MeasurementUnitID { get; set; }

        [Display(Name = "Hạn sử dụng (ngày)")]
        public int? ExpirationDurationDays { get; set; }

        [Required]
        [Display(Name = "Hiển thị")]
        public bool IsActive { get; set; } = true;

        [Required]
        [Display(Name = "Ngày thêm")]
        public DateTime DateAdded { get; set; } = DateTime.Now;

        [Display(Name = "Đường dẫn ảnh")]
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
