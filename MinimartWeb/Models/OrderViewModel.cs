using System;
using System.Collections.Generic;

namespace MinimartWeb.Models
{
    public class OrderViewModel
    {
        public int SaleId { get; set; }
        public DateTime SaleDate { get; set; }
        public string OrderStatus { get; set; } = string.Empty;
        public string PaymentMethodName { get; set; } = string.Empty;
        public string DeliveryAddress { get; set; } = string.Empty;
        public DateTime DeliveryTime { get; set; }
        public bool IsPickup { get; set; }
        public decimal TotalAmount { get; set; }
        public List<OrderItemViewModel> Items { get; set; } = new List<OrderItemViewModel>();
    }
}
