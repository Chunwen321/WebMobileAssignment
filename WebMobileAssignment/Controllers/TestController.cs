using Microsoft.AspNetCore.Mvc;

namespace WebMobileAssignment.Controllers
{
    public class TestController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
