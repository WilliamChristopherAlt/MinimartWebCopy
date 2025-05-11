// Trong ViewModels/ProductViewModel.cs
using System;

namespace MinimartWeb.ViewModels
{
    public class ProductViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ProductDescription { get; set; }
        public decimal Price { get; set; }
        //public decimal? OriginalPrice { get; set; } // ĐÃ BỎ
        public string? ImagePath { get; set; }
        public decimal StockAmount { get; set; }
        public string MeasurementUnitName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime DateAdded { get; set; }
        public int TotalUnitsSold { get; set; }
        public int UnitsSoldThisMonth { get; set; }
        public bool IsFlashSale { get; set; } = false;

        public List<string> Tags { get; set; } = new();

        // public int? DiscountPercent { /* ... property tính toán dựa trên OriginalPrice đã bị bỏ ... */ }
    }
}