using Microsoft.AspNetCore.Mvc;

namespace WebMobileAssignment.Controllers
{
    public class ParentController : Controller
    {
  // Dashboard
        public IActionResult Dashboard()
        {
            ViewBag.ActiveMenu = "Dashboard";
            return View();
        }

        // Attendance - View History
        public IActionResult AttendanceHistory()
{
            ViewBag.ActiveMenu = "Attendance";
      ViewBag.ActiveSubmenu = "History";
            return View();
        }

        // Attendance - Monthly Summary
        public IActionResult MonthlySummary()
        {
   ViewBag.ActiveMenu = "Attendance";
      ViewBag.ActiveSubmenu = "MonthlySummary";
            return View();
        }

        // Classes
        public IActionResult ParentClasses()
        {
            ViewBag.ActiveMenu = "Classes";
            return View();
        }

    // Attendance - Download Report
    public IActionResult DownloadReport()
        {
// Placeholder for PDF generation
         // TODO: Implement PDF generation logic
    return RedirectToAction("MonthlySummary");
 }

        // Student Profile
        public IActionResult StudentProfile()
        {
            ViewBag.ActiveMenu = "StudentProfile";
            return View();
     }

     // Notifications
     public IActionResult Notifications()
  {
         ViewBag.ActiveMenu = "Notifications";
            return View();
     }

 
        // Settings
   public IActionResult Settings()
        {
            ViewBag.ActiveMenu = "Settings";
            return View();
        }
    }
}
