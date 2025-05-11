namespace MinimartWeb.Models
{
    public class OrderItemViewModel
    {
        public string ProductName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal PriceAtPurchase { get; set; }
        public string MeasurementUnit { get; set; } = string.Empty;
        public string? ImagePath { get; set; }
        public decimal Subtotal { get; set; }
    }
}
