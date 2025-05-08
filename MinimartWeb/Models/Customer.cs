using System.ComponentModel.DataAnnotations;

namespace MinimartWeb.Model
{
    public class Customer
    {
        [Key]
        public int CustomerID { get; set; }

        [Required]
        [StringLength(255)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Required]
        [StringLength(255)]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(255)]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [Required]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Phone number must be exactly 10 digits.")]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        [StringLength(512)]
        [Display(Name = "Profile Image")]
        public string? ImagePath { get; set; }

        [Required]
        [StringLength(255)]
        [Display(Name = "Username")]
        public string Username { get; set; }

        [Required]
        [MaxLength(64)]
        [Display(Name = "Password Hash")]
        public byte[] PasswordHash { get; set; }

        [Required]
        [MaxLength(64)]
        [Display(Name = "Salt")]
        public byte[] Salt { get; set; }

        // Navigation property
        public ICollection<Sale> Sales { get; set; } = new HashSet<Sale>();
        public ICollection<OtpRequest> OtpRequests { get; set; } = new HashSet<OtpRequest>();
    }
}
