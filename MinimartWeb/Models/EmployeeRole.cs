using System.ComponentModel.DataAnnotations;

namespace MinimartWeb.Model
{
    public class EmployeeRole
    {
        [Key]
        public int RoleID { get; set; }

        [Required]
        [StringLength(255)]
        [Display(Name = "Role Name")]
        public string RoleName { get; set; }

        [Display(Name = "Role Description")]
        public string? RoleDescription { get; set; }

        // Navigation property
        public ICollection<Employee> Employees { get; set; } = new HashSet<Employee>();
    }
}
