// Trong ViewModels/CustomerProductIndexViewModel.cs
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace MinimartWeb.ViewModels
{
    public class CustomerProductIndexViewModel
    {
        // --- Filter, Sort, Paging cho Kết quả chính ---
        public string? SortOrder { get; set; }
        public string? SearchString { get; set; } // Từ khóa tìm kiếm (nếu có)
        public int? SelectedCategoryId { get; set; }
        public int? SelectedSupplierId { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }

        public List<ProductViewModel> Products { get; set; } = new List<ProductViewModel>(); // Kết quả chính
        public int CurrentPage { get; set; } = 1;    // Trang hiện tại cho Products
        public int TotalPages { get; set; } = 1;     // Tổng số trang cho Products
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;

        // --- Dữ liệu cho Dropdowns Filter ---
        public SelectList? Categories { get; set; }
        public SelectList? Suppliers { get; set; }

        // --- Cho băng chuyền "Sản phẩm gợi ý/cùng loại" ---
        public List<ProductViewModel> RecommendedProducts { get; set; } = new List<ProductViewModel>();
        public string? RecommendationTitle { get; set; } // Tiêu đề cho mục gợi ý

        // --- Cho lưới "Sản phẩm khác" - có phân trang riêng ---
        public List<ProductViewModel> OtherProducts { get; set; } = new List<ProductViewModel>();
        public int CurrentPageOther { get; set; } = 1; // Trang hiện tại cho OtherProducts
        public int TotalPagesOther { get; set; } = 1;  // Tổng số trang cho OtherProducts
        public bool HasPreviousPageOther => CurrentPageOther > 1;
        public bool HasNextPageOther => CurrentPageOther < TotalPagesOther;
    }
}