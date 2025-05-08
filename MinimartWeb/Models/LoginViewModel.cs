using System.ComponentModel.DataAnnotations;

namespace MinimartWeb.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Username is required")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required(ErrorMessage = "User type is required")]
        [Display(Name = "User type")]
        public string UserType { get; set; } // e.g., "Customer" or "Employee"
    }
}
