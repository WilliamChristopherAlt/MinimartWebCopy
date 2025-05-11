// Trong thư mục ViewModels
// Tạo file: SearchResultsViewModel.cs
using System.Collections.Generic;
using MinimartWeb.ViewModels; // Để sử dụng ProductViewModel

namespace MinimartWeb.ViewModels
{
    public class SearchResultsViewModel
    {
        public string Keyword { get; set; } = string.Empty;
        public List<ProductViewModel> SearchResults { get; set; } = new List<ProductViewModel>();
        public List<ProductViewModel> RecommendedProducts { get; set; } = new List<ProductViewModel>();
        public List<ProductViewModel> OtherProducts { get; set; } = new List<ProductViewModel>();

        // Thuộc tính phân trang cho OtherProducts (nếu bạn muốn phân trang ở đây)
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
    }
}