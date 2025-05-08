using MinimartWeb.Model;
using System.ComponentModel.DataAnnotations;

namespace MinimartWeb.Model
{
    public class ProductTag
    {
        [Key]
        public int ProductTagID { get; set; }

        [Required]
        [Display(Name = "Product Type")]
        public int ProductTypeID { get; set; }

        [Required]
        [Display(Name = "Tag")]
        public int TagID { get; set; }

        // Navigation properties
        public ProductType ProductType { get; set; }
        public Tag Tag { get; set; }
    }
}
