using Microsoft.AspNetCore.Mvc;

namespace WebMobileAssignment.Controllers
{
    public class AccountController : Controller
    {
        // GET: /Account/Login
        public IActionResult Login()
        {
            // This is a placeholder - authentication will be implemented later
            ViewBag.Message = "Login page - Authentication will be implemented later";
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        public IActionResult Login(string userType, string email, string password)
        {
            // Placeholder authentication logic
            // In a real application, validate credentials against database
            if (userType == "parent")
            {
                // Redirect parent to their dashboard
                return RedirectToAction("Dashboard", "Parent");
            }
            else if (userType == "admin")
            {
                // Redirect admin to admin dashboard (to be implemented)
                return RedirectToAction("Index", "Home");
            }
            else if (userType == "teacher")
            {
                // Redirect teacher to teacher dashboard
                return RedirectToAction("TeachDashboard", "Teacher");
            }
            else if (userType == "student")
            {
                // Redirect student to student dashboard
                return RedirectToAction("StudDashboard", "Student");
            }

            return View();
        }
    }
}
