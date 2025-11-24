using Microsoft.AspNetCore.Mvc;

namespace WebMobileAssignment.Controllers
{
    public class TeacherController : Controller
    {
        // Dashboard
        public IActionResult TeachDashboard()
        {
            ViewBag.ActiveMenu = "Dashboard";
            return View("TeachDashboard");
        }

        // Attendance - Mark Attendance
        public IActionResult TeachMarkAttendance()
        {
            ViewBag.ActiveMenu = "Attendance";
            ViewBag.ActiveSubmenu = "MarkAttendance";
            return View("TeachMarkAttendance");
        }

        // Attendance - View History
        public IActionResult TeachAttendanceHistory()
        {
            ViewBag.ActiveMenu = "Attendance";
            ViewBag.ActiveSubmenu = "History";
            return View("TeachAttendanceHistory");
        }

        // Classes
        public IActionResult TeachClasses()
        {
            ViewBag.ActiveMenu = "Classes";
            return View("TeachClasses");
        }

        // Class Detail
        public IActionResult TeachClassDetail()
        {
            ViewBag.ActiveMenu = "Classes";
            return View("TeachClassDetail");
        }

        // Students
        public IActionResult TeachStudents()
        {
            ViewBag.ActiveMenu = "Students";
            return View("TeachStudents");
        }

        // Teacher Profile
        public IActionResult TeachProfile()
        {
            ViewBag.ActiveMenu = "Profile";
            return View("TeachProfile");
        }

        // Settings
        public IActionResult TeachSettings()
        {
            ViewBag.ActiveMenu = "Settings";
            return View("TeachSettings");
        }

        // Change Password
        public IActionResult TeachChangePassword()
        {
            ViewBag.ActiveMenu = "Settings";
            ViewBag.ActiveSubmenu = "ChangePassword";
            return View("TeachChangePassword");
        }
    }
}
