using System.ComponentModel.DataAnnotations;

namespace MinimartWeb.Model
{
    public class MeasurementUnit
    {
        [Key]
        public int MeasurementUnitID { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Unit Name")]
        public string UnitName { get; set; }

        [Display(Name = "Unit Description")]
        public string? UnitDescription { get; set; }

        [Required]
        [Display(Name = "Is Continuous")]
        public bool IsContinuous { get; set; }

        // Navigation property
        public ICollection<ProductType> ProductTypes { get; set; } = new HashSet<ProductType>();
    }
}
