// Trong ViewModels/ProductDetailViewModel.cs
using MinimartWeb.Model; // Namespace cho ProductType
using System.Collections.Generic;

namespace MinimartWeb.ViewModels
{
    public class ProductDetailViewModel
    {
        public ProductType Product { get; set; } = null!; // Sản phẩm chính đang xem
        public List<string> Tags { get; set; } = new();
        public List<ProductViewModel> RecommendedProducts { get; set; } = new List<ProductViewModel>();
        public List<ProductViewModel> OtherProducts { get; set; } = new List<ProductViewModel>(); // Hoặc một danh sách ProductType trực tiếp nếu không cần ViewModel phức tạp cho "Other"
        // Thêm các thuộc tính phân trang cho OtherProducts nếu cần
        public int CurrentPageOther { get; set; } = 1;
        public int TotalPagesOther { get; set; }
        public bool HasPreviousPageOther => CurrentPageOther > 1;
        public bool HasNextPageOther => CurrentPageOther < TotalPagesOther;
    }
}