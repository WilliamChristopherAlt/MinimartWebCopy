using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace MinimartWeb.Model
{
    public enum NotificationType
    {
        [Display(Name = "Account Related")]
        AccountRelated,

        [Display(Name = "Order Status Update")]
        OrderStatusUpdate,

        [Display(Name = "Security Alert")]
        SecurityAlert,

        [Display(Name = "New Message")]
        NewMessage,

        [Display(Name = "Promotion")]
        Promotion,

        [Display(Name = "System Message")]
        SystemMessage
    }

    public static class EnumExtensions
    {
        public static string GetDisplayName(this Enum enumValue)
        {
            var displayAttribute = enumValue.GetType()
                                            .GetMember(enumValue.ToString())[0]
                                            .GetCustomAttribute<DisplayAttribute>();

            return displayAttribute != null ? displayAttribute.Name : enumValue.ToString();
        }
    }

    public class Notification
    {
        [Key]
        public int NotificationID { get; set; }

        // 🔥 Mutually Exclusive Fields
        [Display(Name = "Customer ID")]
        public int? CustomerID { get; set; }

        [Display(Name = "Employee Account ID")]
        public int? EmployeeAccountID { get; set; }

        [Display(Name = "Sale ID")]
        public int? SaleID { get; set; }

        [Required]
        [StringLength(255)]
        [Display(Name = "Title")]
        public string Title { get; set; }

        [Required]
        [Display(Name = "Message")]
        public string Message { get; set; }

        [Required]
        [Display(Name = "Created At")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Required]
        [Display(Name = "Is Read")]
        public bool IsRead { get; set; } = false;

        [Required]
        [Display(Name = "Notification Type")]
        public string NotificationType { get; set; }

        // 🔗 Navigation Properties
        [ForeignKey("CustomerID")]
        public Customer? Customer { get; set; }

        [ForeignKey("EmployeeAccountID")]
        public EmployeeAccount? EmployeeAccount { get; set; }

        [ForeignKey("SaleID")]
        public Sale? Sale { get; set; }

        [Display(Name = "Message Customer  ID")]
        public int? MessageCustomerID { get; set; }

        [ForeignKey("MessageCustomerID")]
        public Message? MessageCustomer { get; set; }

        // 🔥 Enforce Mutual Exclusivity
        [NotMapped]
        public bool IsValid => (CustomerID.HasValue ^ EmployeeAccountID.HasValue);
    }
}
