using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MinimartWeb.Model
{
    public class OtpType
    {
        [Key]
        public int OtpTypeID { get; set; }

        [Required]
        [StringLength(255)]
        [Display(Name = "Name")]
        public string OtpTypeName { get; set; }

        [StringLength(255)]
        [Display(Name = "Description")]
        public string Description { get; set; }

        // Navigation property
        public ICollection<OtpRequest> OtpRequests { get; set; } = new List<OtpRequest>();
    }
}
