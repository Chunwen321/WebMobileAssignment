using Microsoft.AspNetCore.Mvc;

namespace WebMobileAssignment.Controllers
{
    public class StudentController : Controller
    {
        // Dashboard
        public IActionResult StudDashboard()
        {
            ViewBag.ActiveMenu = "StudDashboard";
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

        // Student Profile
        public IActionResult StudProfile()
        {
            ViewBag.ActiveMenu = "StudProfile";
            return View("StudProfile");
        }

        // Settings
        public IActionResult StudSettings()
        {
            ViewBag.ActiveMenu = "StudSettings";
            return View("StudSettings");
        }

        // Change Password
        public IActionResult StudChangePassword()
        {
            ViewBag.ActiveMenu = "StudChangePassword";
            return View("StudChangePassword");
        }
    }
}
