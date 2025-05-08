using System.ComponentModel.DataAnnotations;

namespace MinimartWeb.Model
{
    public class Sale
    {
        [Key]
        public int SaleID { get; set; }

        [Required]
        [Display(Name = "Date")]
        public DateTime SaleDate { get; set; } = DateTime.Now;

        [Display(Name = "Customer")]
        public int? CustomerID { get; set; }

        [Required]
        [Display(Name = "Employee")]
        public int EmployeeID { get; set; }

        [Required]
        [Display(Name = "Payment Method")]
        public int PaymentMethodID { get; set; }

        [Required]
        [StringLength(255)]
        [Display(Name = "Delivery Address")]
        public string DeliveryAddress { get; set; }

        [Required]
        [Display(Name = "Delivery Time")]
        public DateTime DeliveryTime { get; set; }

        [Display(Name = "Is Pickup")]
        public bool IsPickup { get; set; } = false;

        [Required]
        [StringLength(50)]
        [RegularExpression(@"^(Pending|Confirmed|Processing|Completed|Cancelled)$", ErrorMessage = "Invalid order status.")]
        [Display(Name = "Order Status")]
        public string OrderStatus { get; set; } = "Pending";

        // Navigation properties
        public Customer Customer { get; set; }
        public Employee Employee { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public ICollection<SaleDetail> SaleDetails { get; set; } = new List<SaleDetail>();
    }
}
