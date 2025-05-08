// Trong thư mục ViewModels (tạo nếu chưa có)
namespace MinimartWeb.ViewModels // Đảm bảo namespace đúng
{
    public class ProductViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? OriginalPrice { get; set; } // Giá gốc (nếu có giảm giá)
        public string? ImagePath { get; set; }
        public int TotalUnitsSold { get; set; } // Tổng số lượng đã bán (cho mục "Đã bán")
        public int UnitsSoldThisMonth { get; set; } // Số lượng bán trong tháng (để xếp hạng Hot Deal)

        // Property tính toán phần trăm giảm giá (nếu có)
        public int? DiscountPercent
        {
            get
            {
                if (OriginalPrice.HasValue && OriginalPrice > Price && OriginalPrice != 0)
                {
                    return (int)Math.Round(((OriginalPrice.Value - Price) / OriginalPrice.Value) * 100);
                }
                return null;
            }
        }
        // Có thể thêm các thuộc tính khác nếu cần (CategoryName, SupplierName, IsFlashSale...)
        public bool IsFlashSale { get; set; } = false; // Ví dụ cờ Flash Sale
    }
}