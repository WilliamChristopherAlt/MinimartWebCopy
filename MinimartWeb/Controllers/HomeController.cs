using Microsoft.AspNetCore.Mvc;

namespace MinimartWeb.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return RedirectToAction("Index", "ProductTypes");
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}
