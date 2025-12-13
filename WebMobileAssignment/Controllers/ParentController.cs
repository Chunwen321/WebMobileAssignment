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
   
                // Get unread notification count
   var unreadCount = await _context.Notifications
.CountAsync(n => n.UserId == parent.UserId && n.Status == "unread");
           ViewBag.UnreadNotificationCount = unreadCount;
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
        public async Task<IActionResult> AttendanceHistory(string? classId, string? month, string? status)
        {
   ViewBag.ActiveMenu = "Attendance";
            ViewBag.ActiveSubmenu = "History";
   
          var parent = await GetCurrentParentAsync();
            
   if (parent == null)
      {
                return RedirectToAction("Login", "Account");
 }
        
 // Get all students for this parent
            var students = await _context.Students
     .Include(s => s.User)
  .Include(s => s.Enrollments)
           .ThenInclude(e => e.Class)
   .ThenInclude(c => c.Subject)
    .Where(s => s.ParentId == parent.ParentId)
    .ToListAsync();
       
            if (!students.Any())
  {
       ViewBag.Students = new List<Student>();
        ViewBag.Attendances = new List<Attendance>();
  ViewBag.Classes = new List<Class>();
       ViewBag.TotalPresent = 0;
     ViewBag.TotalAbsent = 0;
         ViewBag.TotalLate = 0;
         ViewBag.AttendanceRate = 0;
                return View();
 }
    
    // Get all enrolled classes for filter dropdown
            var enrolledClasses = students
    .SelectMany(s => s.Enrollments.Select(e => e.Class))
                .Distinct()
  .ToList();
            
            // Get student IDs
            var studentIds = students.Select(s => s.StudentId).ToList();
            
          // Build attendance query
     var query = _context.Attendances
     .Include(a => a.Student)
      .ThenInclude(s => s.User)
      .Include(a => a.Class)
     .ThenInclude(c => c.Subject)
     .Include(a => a.MarkedByTeacher)
       .ThenInclude(t => t.User)
        .Where(a => studentIds.Contains(a.StudentId));
            
  // Apply filters
  if (!string.IsNullOrEmpty(classId))
{
       query = query.Where(a => a.ClassId == classId);
  ViewBag.SelectedClassId = classId;
 }
  
    if (!string.IsNullOrEmpty(month))
            {
    if (DateTime.TryParse(month + "-01", out DateTime monthDate))
   {
              var startDate = new DateTime(monthDate.Year, monthDate.Month, 1);
 var endDate = startDate.AddMonths(1).AddDays(-1);
   query = query.Where(a => a.Date >= startDate && a.Date <= endDate);
               ViewBag.SelectedMonth = month;
    }
      }
            
     if (!string.IsNullOrEmpty(status))
            {
     query = query.Where(a => a.Status == status);
    ViewBag.SelectedStatus = status;
            }
     
            // Get filtered attendances
   var attendances = await query
       .OrderByDescending(a => a.Date)
        .Take(50)
           .ToListAsync();
            
       // Calculate statistics
            var totalPresent = attendances.Count(a => a.Status == "Present");
  var totalAbsent = attendances.Count(a => a.Status == "Absent");
            var totalLate = attendances.Count(a => a.Status == "Late");
            var totalCount = attendances.Count;
        var attendanceRate = totalCount > 0 ? Math.Round((decimal)totalPresent / totalCount * 100, 1) : 0;
     
         ViewBag.Students = students;
    ViewBag.Attendances = attendances;
            ViewBag.Classes = enrolledClasses;
     ViewBag.TotalPresent = totalPresent;
            ViewBag.TotalAbsent = totalAbsent;
    ViewBag.TotalLate = totalLate;
    ViewBag.AttendanceRate = attendanceRate;
 
     return View();
        }

        // Attendance - Monthly Summary
        public async Task<IActionResult> MonthlySummary(int? year, int? month)
    {
     ViewBag.ActiveMenu = "Attendance";
            ViewBag.ActiveSubmenu = "MonthlySummary";
       
            var parent = await GetCurrentParentAsync();
            
       if (parent == null)
         {
     return RedirectToAction("Login", "Account");
    }
  
   // Default to current month if not specified
        var selectedYear = year ?? DateTime.Now.Year;
            var selectedMonth = month ?? DateTime.Now.Month;
   var startDate = new DateTime(selectedYear, selectedMonth, 1);
  var endDate = startDate.AddMonths(1).AddDays(-1);
       
   ViewBag.SelectedYear = selectedYear;
            ViewBag.SelectedMonth = selectedMonth;
     ViewBag.MonthName = startDate.ToString("MMMM yyyy");
            
      // Get all students for this parent
   var students = await _context.Students
           .Include(s => s.User)
         .Include(s => s.Enrollments)
         .ThenInclude(e => e.Class)
.ThenInclude(c => c.Subject)
            .Include(s => s.Attendances)
  .Where(s => s.ParentId == parent.ParentId)
              .ToListAsync();
       
    if (!students.Any())
         {
       ViewBag.TotalClasses = 0;
   ViewBag.TotalPresent = 0;
                ViewBag.TotalAbsent = 0;
                ViewBag.TotalLate = 0;
   ViewBag.AttendanceRate = 0;
       ViewBag.SubjectSummary = new List<object>();
           return View();
    }
     
        // Get student IDs
     var studentIds = students.Select(s => s.StudentId).ToList();
  
      // Get attendances for the selected month
   var monthlyAttendances = await _context.Attendances
   .Include(a => a.Class)
       .ThenInclude(c => c.Subject)
        .Where(a => studentIds.Contains(a.StudentId) && 
         a.Date >= startDate && 
       a.Date <= endDate)
    .ToListAsync();
  
// Calculate overall statistics
            var totalClasses = monthlyAttendances.Count;
       var totalPresent = monthlyAttendances.Count(a => a.Status == "Present");
            var totalAbsent = monthlyAttendances.Count(a => a.Status == "Absent");
  var totalLate = monthlyAttendances.Count(a => a.Status == "Late");
            var attendanceRate = totalClasses > 0 ? Math.Round((decimal)totalPresent / totalClasses * 100, 1) : 0;
            
// Calculate subject-wise summary
        var subjectSummary = monthlyAttendances
       .GroupBy(a => new { 
            SubjectId = a.Class.SubjectId, 
     SubjectName = a.Class.Subject?.SubjectName ?? "N/A" 
    })
            .Select(g => new
        {
          Subject = g.Key.SubjectName,
          TotalClasses = g.Count(),
   Present = g.Count(a => a.Status == "Present"),
      Absent = g.Count(a => a.Status == "Absent"),
    Late = g.Count(a => a.Status == "Late"),
 AttendanceRate = g.Count() > 0 ? Math.Round((decimal)g.Count(a => a.Status == "Present") / g.Count() * 100, 1) : 0
         })
           .OrderBy(s => s.Subject)
              .ToList();
            
            ViewBag.TotalClasses = totalClasses;
            ViewBag.TotalPresent = totalPresent;
            ViewBag.TotalAbsent = totalAbsent;
       ViewBag.TotalLate = totalLate;
            ViewBag.AttendanceRate = attendanceRate;
        ViewBag.SubjectSummary = subjectSummary;
            
            return View();
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
            ViewBag.ActiveMenu = "Classes";
            
            var parent = await GetCurrentParentAsync();
          
 if (parent == null)
            {
                return RedirectToAction("Login", "Account");
         }
     
// Get all students (children) for this parent with their enrollments and classes
  var students = await _context.Students
           .Include(s => s.User)
          .Include(s => s.Enrollments)
   .ThenInclude(e => e.Class)
  .ThenInclude(c => c.Teacher)
        .ThenInclude(t => t.User)
            .Include(s => s.Enrollments)
          .ThenInclude(e => e.Class)
  .ThenInclude(c => c.Subject)
     .Include(s => s.Attendances)
       .ThenInclude(a => a.Class)
       .Where(s => s.ParentId == parent.ParentId)
      .ToListAsync();
    
            ViewBag.Students = students;
          
            return View("ParentClasses");
        }

        // Class Detail
        public async Task<IActionResult> ParentClassDetail(string classId, string studentId)
   {
 ViewBag.ActiveMenu = "Classes";
            
            var parent = await GetCurrentParentAsync();
  
   if (parent == null)
            {
      return RedirectToAction("Login", "Account");
            }
      
         // Verify the student belongs to this parent
            var student = await _context.Students
          .Include(s => s.User)
                .Include(s => s.Enrollments)
           .ThenInclude(e => e.Class)
     .ThenInclude(c => c.Teacher)
             .ThenInclude(t => t.User)
     .Include(s => s.Enrollments)
   .ThenInclude(e => e.Class)
         .ThenInclude(c => c.Subject)
    .Include(s => s.Attendances)
        .Where(s => s.StudentId == studentId && s.ParentId == parent.ParentId)
       .FirstOrDefaultAsync();
            
  if (student == null)
    {
        TempData["Error"] = "Student not found or access denied.";
 return RedirectToAction("ParentClasses");
     }
     
            // Get the specific class enrollment
         var enrollment = student.Enrollments.FirstOrDefault(e => e.ClassId == classId);
            
            if (enrollment == null)
      {
 TempData["Error"] = "Class not found for this student.";
          return RedirectToAction("ParentClasses");
        }
            
            // Get attendance records for this student and class
          var attendances = await _context.Attendances
       .Include(a => a.Class)
  .Where(a => a.StudentId == studentId && a.ClassId == classId)
       .OrderByDescending(a => a.Date)
       .ToListAsync();
  
            ViewBag.Student = student;
     ViewBag.Class = enrollment.Class;
      ViewBag.Attendances = attendances;
         
      return View("ParentClassDetail");
   }

        // Notifications
 public async Task<IActionResult> Notifications()
        {
     ViewBag.ActiveMenu = "Notifications";
   
 var parent = await GetCurrentParentAsync();
        
  if (parent == null)
            {
     return RedirectToAction("Login", "Account");
         }
     
  // Get notifications for this parent user
        var notifications = await _context.Notifications
.Include(n => n.User)
              .Where(n => n.UserId == parent.UserId)
      .OrderByDescending(n => n.CreatedDate)
       .ToListAsync();
  
            // Calculate notification stats
            var totalNotifications = notifications.Count;
   var unreadCount = notifications.Count(n => n.Status == "unread");
            var readCount = notifications.Count(n => n.Status == "read");
          
// Count warnings (notifications containing "late", "absent", or "warning" in description)
            var warningCount = notifications.Count(n => 
     n.Description.ToLower().Contains("late") || 
n.Description.ToLower().Contains("absent") || 
                n.Description.ToLower().Contains("warning"));
          
   ViewBag.TotalNotifications = totalNotifications;
            ViewBag.UnreadCount = unreadCount;
     ViewBag.ReadCount = readCount;
 ViewBag.WarningCount = warningCount;
            ViewBag.Notifications = notifications;
   
         return View();
        }
        
        // Mark notification as read
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(string notificationId)
        {
            try
     {
                var parent = await GetCurrentParentAsync();
  
       if (parent == null)
        {
     return Json(new { success = false, message = "Unauthorized" });
      }
            
  var notification = await _context.Notifications
   .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == parent.UserId);
         
      if (notification == null)
      {
     return Json(new { success = false, message = "Notification not found" });
      }
      
    notification.Status = "read";
      await _context.SaveChangesAsync();
                
        return Json(new { success = true });
     }
    catch (Exception ex)
            {
    return Json(new { success = false, message = ex.Message });
 }
        }
        
        // Mark all notifications as read
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAsRead()
        {
   try
        {
              var parent = await GetCurrentParentAsync();
   
                if (parent == null)
              {
      return Json(new { success = false, message = "Unauthorized" });
      }
         
      var notifications = await _context.Notifications
   .Where(n => n.UserId == parent.UserId && n.Status == "unread")
   .ToListAsync();
 
     foreach (var notification in notifications)
         {
   notification.Status = "read";
       }
  
         await _context.SaveChangesAsync();
    
     return Json(new { success = true, count = notifications.Count });
     }
      catch (Exception ex)
            {
      return Json(new { success = false, message = ex.Message });
        }
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

        // Download Report - Optional PDF generation functionality
   public IActionResult DownloadReport()
  {
            // TODO: Implement PDF generation logic in the future if needed
   // For now, redirect back to Monthly Summary
        TempData["Info"] = "PDF download feature will be available soon.";
 return RedirectToAction("MonthlySummary");
        }
    }
}
