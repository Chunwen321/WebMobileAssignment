using Microsoft.AspNetCore.Mvc;

namespace WebMobileAssignment.Controllers
{
    public class HomeController : Controller
    {
        // GET: /Home/Index
        public IActionResult Index()
        {
            ViewData["Title"] = "Home";
            return View();
        }

        // GET: /Home/Error
        public IActionResult Error()
        {
            return View();
        }
    }
}
