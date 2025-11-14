using Microsoft.AspNetCore.Mvc;

namespace WebMobileAssignment.Controllers
{
    public class AccountController : Controller
    {
        // GET: /Account/Login
        public IActionResult Login()
      {
            // This is a placeholder - authentication will be implemented later
            ViewData["Message"] = "Login page - Authentication will be implemented later";
          return View();
        }
    }
}
