// Trong thư mục ViewModels
using System.Collections.Generic;

namespace MinimartWeb.ViewModels // Đảm bảo namespace đúng
{
    public class HomePageViewModel
    {
        public List<ProductViewModel> HotDeals { get; set; } = new List<ProductViewModel>();
        public List<ProductViewModel> RegularProducts { get; set; } = new List<ProductViewModel>();
    }
}