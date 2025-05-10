// Trong thư mục: ViewModels
// Tên file: CustomerProductIndexViewModel.cs
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering; // Cho SelectList

namespace MinimartWeb.ViewModels
{
    public class CustomerProductIndexViewModel
    {
        public List<ProductViewModel> Products { get; set; } = new List<ProductViewModel>();

        // Thông tin phân trang
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;

        // Thông tin sắp xếp và lọc hiện tại để giữ lại khi chuyển trang/sắp xếp
        public string? SortOrder { get; set; }
        public string? SearchString { get; set; }
        public int? SelectedCategoryId { get; set; }
        public int? SelectedSupplierId { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }

        // SelectList cho dropdown bộ lọc (sẽ được gán từ Controller)
        public SelectList? Categories { get; set; }
        public SelectList? Suppliers { get; set; }
    }
}