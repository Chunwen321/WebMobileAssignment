using Microsoft.AspNetCore.Mvc;

namespace WebMobileAssignment.Controllers
{
    public class StudentController : Controller
    {
        // Dashboard
        public IActionResult StudDashboard()
        {
            ViewBag.ActiveMenu = "Dashboard";
            return View("StudDashboard");
        }

        // Attendance History
        public IActionResult StudAttendanceHistory()
        {
            ViewBag.ActiveMenu = "Attendance";
            ViewBag.ActiveSubmenu = "History";
            return View("StudAttendanceHistory");
        }

        // Classes
        public IActionResult StudClasses()
        {
            ViewBag.ActiveMenu = "Classes";
            return View("StudClasses");
        }

        // Take Attendance
        public IActionResult StudTakeAttendance()
        {
            ViewBag.ActiveMenu = "Attendance";
            ViewBag.ActiveSubmenu = "TakeAttendance";
            return View("StudTakeAttendance");
        }

        // Student Profile
        public IActionResult StudProfile()
        {
            ViewBag.ActiveMenu = "Profile";
            return View("StudProfile");
        }

        // Settings
        public IActionResult StudSettings()
        {
            ViewBag.ActiveMenu = "Settings";
            return View("StudSettings");
        }

        // Change Password
        public IActionResult StudChangePassword()
        {
            ViewBag.ActiveMenu = "Settings";
            ViewBag.ActiveSubmenu = "ChangePassword";
            return View("StudChangePassword");
        }
    }
}
