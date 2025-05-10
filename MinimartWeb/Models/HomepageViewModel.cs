// Trong ViewModels/HomePageViewModel.cs
using System.Collections.Generic;

namespace MinimartWeb.ViewModels
{
    public class HomePageViewModel
    {
        public List<ProductViewModel> HotDeals { get; set; } = new List<ProductViewModel>();
        public List<ProductViewModel> RecommendedProducts { get; set; } = new List<ProductViewModel>();
        public List<ProductViewModel> RegularProducts { get; set; } = new List<ProductViewModel>();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
    }
}