using Microsoft.AspNetCore.Mvc;

namespace WebMobileAssignment.Controllers
{
    public class HomeController : Controller
    {
        // GET: /Home/Index
        public IActionResult Index()
        {
            ViewBag.Title = "Home";
            return View();
        }

        // GET: /Home/Error
        public IActionResult Error()
        {
            return View();
        }

        // GET: /Home/Student
        public IActionResult Student()
        {
            return View();
        }
    }
}
