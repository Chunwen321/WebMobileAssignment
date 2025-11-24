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

        // Take Attendance
        public IActionResult StudTakeAttendance()
        {
            ViewBag.ActiveMenu = "Attendance";
            ViewBag.ActiveSubmenu = "TakeAttendance";
            return View("StudTakeAttendance");
        }

        // Classes
        public IActionResult StudClasses()
        {
            ViewBag.ActiveMenu = "Classes";
            return View("StudClasses");
        }

        // Class Detail
        public IActionResult StudClassDetail()
        {
            ViewBag.ActiveMenu = "Classes";
            return View("StudClassDetail");
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
            ViewBag.ActiveSubmenu = "Settings";
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
