using System;
using System.Collections.Generic;

namespace MinimartWeb.Models
{
    public class OrderDetailViewModel
    {
        public int SaleId { get; set; }
        public DateTime SaleDate { get; set; }
        public string OrderStatus { get; set; } = string.Empty;

        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }
        public string? CustomerPhone { get; set; }

        public string? EmployeeName { get; set; }

        public string PaymentMethodName { get; set; } = string.Empty;
        public string DeliveryAddress { get; set; } = string.Empty;
        public DateTime DeliveryTime { get; set; }
        public bool IsPickup { get; set; }
        public decimal TotalAmount { get; set; }

        public string? CancellationReason { get; set; } // Add this line


        public List<OrderItemViewModel> Items { get; set; } = new List<OrderItemViewModel>();
    }
}
