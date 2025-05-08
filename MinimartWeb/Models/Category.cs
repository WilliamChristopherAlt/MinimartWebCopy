using System.ComponentModel.DataAnnotations;

namespace MinimartWeb.Model
{
    public class Category
    {
        [Key]
        public int CategoryID { get; set; }

        [Required]
        [StringLength(255)]
        [Display(Name = "Name")]
        public string CategoryName { get; set; }

        [Display(Name = "Description")]
        public string? CategoryDescription { get; set; }

        // Navigation property
        public ICollection<ProductType> ProductTypes { get; set; } = new HashSet<ProductType>();
    }
}
