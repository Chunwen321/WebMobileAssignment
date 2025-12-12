using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebMobileAssignment.Models;

namespace WebMobileAssignment.Controllers
{
    public class ParentController : Controller
    {
        private readonly DB _context;
        private readonly Helper _helper;

        public ParentController(DB context, Helper helper)
        {
            _context = context;
            _helper = helper;
        }

        // Helper method to get current parent and set ViewBag data
        private async Task<Parent?> GetCurrentParentAsync()
        {
            var email = User.Identity?.Name;
     
            if (string.IsNullOrEmpty(email))
            {
                // No authenticated user - return null instead of fallback
                return null;
            }

            var parent = await _context.Parents
                .Include(p => p.User)
                .Include(p => p.Students)
                    .ThenInclude(s => s.User)
                .Include(p => p.Students)
                    .ThenInclude(s => s.Enrollments)
                    .ThenInclude(e => e.Class)
                .Include(p => p.Students)
                    .ThenInclude(s => s.Attendances)
                .FirstOrDefaultAsync(p => p.User.Email == email);
      
            // Set ViewBag for layout
            if (parent != null)
            {
                ViewBag.ParentName = parent.User.FullName;
            }
       
            return parent;
        }

        // Dashboard
        public async Task<IActionResult> Dashboard()
        {
            ViewBag.ActiveMenu = "Dashboard";
            
            var parent = await GetCurrentParentAsync();
              
            if (parent != null && parent.Students.Any())
            {
                // Get the first student (primary child)
                var student = parent.Students.FirstOrDefault();
                
                if (student != null)
                {
                    // Calculate attendance statistics
                    var allAttendances = student.Attendances;
                    var totalAttendance = allAttendances.Count;
                    var presentCount = allAttendances.Count(a => a.Status == "Present");
                    var absentCount = allAttendances.Count(a => a.Status == "Absent");
                    var lateCount = allAttendances.Count(a => a.Status == "Late");
                    var attendanceRate = totalAttendance > 0 ? Math.Round((decimal)presentCount / totalAttendance * 100, 1) : 0;
                    
                    // Get recent attendance (last 5 records)
                    var recentAttendance = await _context.Attendances
                            .Include(a => a.Class)
                            .Include(a => a.Student)
                            .Where(a => a.StudentId == student.StudentId)
                            .OrderByDescending(a => a.Date)
                            .Take(5)
                            .ToListAsync();
                
                    ViewBag.StudentName = student.User.FullName;
                    ViewBag.StudentId = student.StudentId;
                    ViewBag.TotalClasses = student.Enrollments.Count;
                    ViewBag.TotalAttendance = totalAttendance;
                    ViewBag.PresentCount = presentCount;
                    ViewBag.AbsentCount = absentCount;
                    ViewBag.LateCount = lateCount;
                    ViewBag.AttendanceRate = attendanceRate;
                    ViewBag.RecentAttendance = recentAttendance;
                    
                    // Get primary class info
                    var primaryEnrollment = student.Enrollments.FirstOrDefault();
                    if (primaryEnrollment != null)
                    {
                        ViewBag.ClassName = primaryEnrollment.Class.ClassName;
                    }
                }
            }
              
            return View();
        }

        // Attendance - View History
        public async Task<IActionResult> AttendanceHistory()
        {
            await GetCurrentParentAsync();  // Set ViewBag.ParentName
            ViewBag.ActiveMenu = "Attendance";
            ViewBag.ActiveSubmenu = "History";
            return View();
        }

        // Attendance - Monthly Summary
        public async Task<IActionResult> MonthlySummary()
        {
            await GetCurrentParentAsync();  // Set ViewBag.ParentName
            ViewBag.ActiveMenu = "Attendance";
            ViewBag.ActiveSubmenu = "MonthlySummary";
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
        public async Task<IActionResult> StudentProfile()
        {
            ViewBag.ActiveMenu = "StudentProfile";
     
            var parent = await GetCurrentParentAsync();
            
            if (parent != null && parent.Students.Any())
            {
                var student = parent.Students.FirstOrDefault();
                ViewBag.Student = student;
                ViewBag.Parent = parent;
                
                if (student != null)
                {
                    // Calculate attendance statistics
                    var allAttendances = student.Attendances;
                    var totalAttendance = allAttendances.Count;
                    var presentCount = allAttendances.Count(a => a.Status == "Present");
                    var absentCount = allAttendances.Count(a => a.Status == "Absent");
                    var lateCount = allAttendances.Count(a => a.Status == "Late");
                    var attendanceRate = totalAttendance > 0 ? Math.Round((decimal)presentCount / totalAttendance * 100, 1) : 0;
                
                    ViewBag.TotalAttendance = totalAttendance;
                    ViewBag.PresentCount = presentCount;
                    ViewBag.AbsentCount = absentCount;
                    ViewBag.LateCount = lateCount;
                    ViewBag.AttendanceRate = attendanceRate;
                }
            }
      
            return View();
        }

        // Classes
        public async Task<IActionResult> ParentClasses()
        {
            await GetCurrentParentAsync();  // Set ViewBag.ParentName
            ViewBag.ActiveMenu = "Classes";
            return View("ParentClasses");
        }

        // Class Detail
        public async Task<IActionResult> ParentClassDetail()
        {
            await GetCurrentParentAsync();  // Set ViewBag.ParentName
            ViewBag.ActiveMenu = "Classes";
            return View("ParentClassDetail");
        }

        // Notifications
        public async Task<IActionResult> Notifications()
        {
            await GetCurrentParentAsync();  // Set ViewBag.ParentName
            ViewBag.ActiveMenu = "Notifications";
            return View();
        }

        // Settings
        public async Task<IActionResult> Settings()
        {
            ViewBag.ActiveMenu = "Settings";
        
            var parent = await GetCurrentParentAsync();
            ViewBag.Parent = parent;
            return View();
        }

        // Update Profile
        [HttpPost]
        [ValidateAntiForgeryToken]
      public async Task<IActionResult> UpdateProfile(string fullName, string email, string? phoneNumber, 
            DateTime? dateOfBirth, string? gender, string? address)
        {
     try
     {
     var parent = await GetCurrentParentAsync();
         
      if (parent == null)
      {
  TempData["Error"] = "Parent profile not found.";
         return RedirectToAction("Settings");
         }

                // Update User table fields
         parent.User.FullName = fullName;
       // Email is readonly, so we don't update it
    parent.User.PhoneNumber = phoneNumber;
  parent.User.DateOfBirth = dateOfBirth;
   parent.User.Gender = gender;
        
      // Update Parent table fields
     parent.PhoneNumber = phoneNumber;
        parent.Address = address;
     
          // Save changes to database
 await _context.SaveChangesAsync();
   
             TempData["Success"] = "Profile updated successfully!";
    }
            catch (Exception ex)
    {
           TempData["Error"] = $"Error updating profile: {ex.Message}";
      }
            
  return RedirectToAction("Settings");
        }

        // Change Password
        [HttpPost]
        [ValidateAntiForgeryToken]
 public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
  try
     {
       var parent = await GetCurrentParentAsync();

    if (parent == null)
       {
   TempData["Error"] = "Parent profile not found.";
        return RedirectToAction("Settings");
      }

      // Verify new password and confirm password match
      if (newPassword != confirmPassword)
    {
                TempData["Error"] = "New password and confirm password do not match.";
  return RedirectToAction("Settings");
    }

        // Verify password length
    if (newPassword.Length < 8)
           {
         TempData["Error"] = "Password must be at least 8 characters long.";
          return RedirectToAction("Settings");
  }

      // Verify current password using Helper (handles both plain text and hashed passwords)
            bool isPasswordCorrect = false;
   
        // Check if password is hashed (ASP.NET Identity hashes start with "AQA" or are longer than 50 chars)
     if (parent.User.PasswordHash.StartsWith("AQA") || parent.User.PasswordHash.Length > 50)
      {
      // Password is hashed - use Helper.VerifyPassword
      isPasswordCorrect = _helper.VerifyPassword(parent.User.PasswordHash, currentPassword);
}
                else
      {
        // Password is plain text - compare directly (for backward compatibility)
        isPasswordCorrect = parent.User.PasswordHash == currentPassword;
         }

                if (!isPasswordCorrect)
    {
        TempData["Error"] = "Current password is incorrect.";
       return RedirectToAction("Settings");
        }

      // Hash and update new password using Helper
      parent.User.PasswordHash = _helper.HashPassword(newPassword);
    
     // Save changes to database
                await _context.SaveChangesAsync();
     
          TempData["Success"] = "Password changed successfully!";
     }
            catch (Exception ex)
  {
          TempData["Error"] = $"Error changing password: {ex.Message}";
            }
            
     return RedirectToAction("Settings");
        }
    }
}
