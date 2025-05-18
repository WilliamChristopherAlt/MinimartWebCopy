using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MinimartWeb.Model
{
    public class Message
    {
        [Key]
        public int MessageID { get; set; }

        [Required]
        public int CustomerID { get; set; }

        [Required]
        [Display(Name = "From Customer?")]
        public bool IsFromCustomer { get; set; }

        [Required]
        [Display(Name = "Message")]
        public byte[] MessageText { get; set; }

        [Required]
        [Display(Name = "Sent At")]
        public DateTime SentAt { get; set; } = DateTime.Now;

        [Display(Name = "Read At")]
        public DateTime? ReadAt { get; set; }

        [Required]
        [Display(Name = "Is Read")]
        public bool IsRead { get; set; } = false;

        [Required]
        [Display(Name = "Deleted By Sender")]
        public bool IsDeletedBySender { get; set; } = false;

        [Required]
        [Display(Name = "Deleted By Receiver")]
        public bool IsDeletedByReceiver { get; set; } = false;

        // Navigation property
        [ForeignKey(nameof(CustomerID))]
        public Customer Customer { get; set; }
    }
}
