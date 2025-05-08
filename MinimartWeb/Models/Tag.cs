using System.ComponentModel.DataAnnotations;

namespace MinimartWeb.Model
{
    public class Tag
    {
        [Key]
        public int TagID { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Tag Name")]
        public string TagName { get; set; }

        // Navigation property
        public ICollection<ProductTag> ProductTags { get; set; } = new List<ProductTag>();
    }
}
