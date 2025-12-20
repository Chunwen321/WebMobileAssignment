using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebMobileAssignment.Models;
using Microsoft.AspNetCore.Authorization;
using WebMobileAssignment.Services;

namespace WebMobileAssignment.Controllers
{
  [Authorize(Roles = "Parent")]
    public class ParentController : Controller
    {
        private readonly DB _context;
        private readonly Helper _helper;
        private readonly S3Service _s3Service;
        private readonly ReportService _reportService;

        public ParentController(DB context, Helper helper, S3Service s3Service, ReportService reportService)
      {
         _context = context;
       _helper = helper;
      _s3Service = s3Service;
        _reportService = reportService;
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
     .ThenInclude(c => c.Teacher)
      .ThenInclude(t => t.User)
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
        public async Task<IActionResult> Dashboard(string? studentId)
  {
   ViewBag.ActiveMenu = "Dashboard";
            
   var parent = await GetCurrentParentAsync();
     
   if (parent != null && parent.Students.Any())
       {
    // Get all students for navigation
      var allStudents = parent.Students.ToList();
     ViewBag.AllStudents = allStudents;
 ViewBag.TotalChildren = allStudents.Count;
  
          // Get the selected student (either from parameter or first student)
        Student? student = null;
 if (!string.IsNullOrEmpty(studentId))
    {
     student = allStudents.FirstOrDefault(s => s.StudentId == studentId);
           }
      
      // If still null, default to first student
  if (student == null)
 {
         student = allStudents.FirstOrDefault();
 }
       
     if (student != null)
    {
      // Get current student index for navigation
            var currentIndex = allStudents.FindIndex(s => s.StudentId == student.StudentId);
       ViewBag.CurrentIndex = currentIndex;
        ViewBag.CurrentStudentId = student.StudentId;
 
        // Check if there are previous/next students
    ViewBag.HasPrevious = currentIndex > 0;
   ViewBag.HasNext = currentIndex < allStudents.Count - 1;
      ViewBag.PreviousStudentId = ViewBag.HasPrevious ? allStudents[currentIndex - 1].StudentId : null;
ViewBag.NextStudentId = ViewBag.HasNext ? allStudents[currentIndex + 1].StudentId : null;
     
  // Calculate attendance statistics (Last 30 days)
      var thirtyDaysAgo = DateTime.Now.AddDays(-30).Date;
      var last30DaysAttendances = student.Attendances.Where(a => a.Date >= thirtyDaysAgo).ToList();
      var totalAttendance = last30DaysAttendances.Count;
        var presentCount = last30DaysAttendances.Count(a => a.Status == "Present");
         var absentCount = last30DaysAttendances.Count(a => a.Status == "Absent");
  var leaveCount = last30DaysAttendances.Count(a => a.Status == "Leave");
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
   ViewBag.TotalClasses = student.Enrollments.Count;
        ViewBag.TotalAttendance = totalAttendance;
        ViewBag.PresentCount = presentCount;
        ViewBag.AbsentCount = absentCount;
     ViewBag.LeaveCount = leaveCount;
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

        // AJAX endpoint to get dashboard data for a specific child
    [HttpGet]
 public async Task<IActionResult> GetDashboardData(string studentId, int days = 30)
        {
     try
  {
      var parent = await GetCurrentParentAsync();
  
      if (parent == null || !parent.Students.Any())
    {
 return Json(new { success = false, message = "No parent or students found" });
   }

 // Get all students for navigation
      var allStudents = parent.Students.ToList();
          
 // Find the requested student
  var student = allStudents.FirstOrDefault(s => s.StudentId == studentId);
             
      if (student == null)
  {
    return Json(new { success = false, message = "Student not found" });
      }

    // Get current student index for navigation
       var currentIndex = allStudents.FindIndex(s => s.StudentId == studentId);
  
    // Calculate attendance statistics with date filter
var daysAgo = DateTime.Now.AddDays(-days).Date;
var filteredAttendances = student.Attendances.Where(a => a.Date >= daysAgo).ToList();
     var totalAttendance = filteredAttendances.Count;
    var presentCount = filteredAttendances.Count(a => a.Status == "Present");
       var absentCount = filteredAttendances.Count(a => a.Status == "Absent");
    var leaveCount = filteredAttendances.Count(a => a.Status == "Leave");
    var attendanceRate = totalAttendance > 0 ? Math.Round((decimal)presentCount / totalAttendance * 100, 1) : 0;
    
   // Get recent attendance (last 5 records)
var recentAttendance = await _context.Attendances
.Include(a => a.Class)
        .Where(a => a.StudentId == student.StudentId)
     .OrderByDescending(a => a.Date)
       .Take(5)
 .Select(a => new {
  date = a.Date.ToString("yyyy-MM-dd"),
      className = a.Class.ClassName,
       timeIn = a.TakenOn.ToString("h:mm tt"),
            status = a.Status
   })
       .ToListAsync();
       
     // Get primary class info
var primaryEnrollment = student.Enrollments.FirstOrDefault();
      var className = primaryEnrollment != null ? primaryEnrollment.Class.ClassName : "Not enrolled";
    
  // Calculate rates for chart
var presentRate = totalAttendance > 0 ? Math.Round((decimal)presentCount / totalAttendance * 100, 1) : 0;
   var absentRate = totalAttendance > 0 ? Math.Round((decimal)absentCount / totalAttendance * 100, 1) : 0;
            var leaveRate = totalAttendance > 0 ? Math.Round((decimal)leaveCount / totalAttendance * 100, 1) : 0;

   return Json(new
    {
 success = true,
        data = new
        {
 // Student info
   studentName = student.User.FullName,
   studentId = student.StudentId,
          className = className,
  currentIndex = currentIndex + 1,
  totalChildren = allStudents.Count,
      profilePicture = student.User.ProfilePicture ?? "/images/default-avatar.png",
    
   // Navigation
      hasPrevious = currentIndex > 0,
    hasNext = currentIndex < allStudents.Count - 1,
      previousStudentId = currentIndex > 0 ? allStudents[currentIndex - 1].StudentId : null,
 nextStudentId = currentIndex < allStudents.Count - 1 ? allStudents[currentIndex + 1].StudentId : null,
       
            // Stats
       totalClasses = student.Enrollments.Count,
       totalAttendance = totalAttendance,
       presentCount = presentCount,
    absentCount = absentCount,
          leaveCount = leaveCount,
            attendanceRate = attendanceRate,
  
 // Chart data
      presentRate = presentRate,
    absentRate = absentRate,
    leaveRate = leaveRate,
   
  // Recent attendance
  recentAttendance = recentAttendance
    }
          });
  }
   catch (Exception ex)
   {
       return Json(new { success = false, message = ex.Message });
      }
        }

        // AJAX endpoint to get filtered attendance history data
        [HttpGet]
        public async Task<IActionResult> GetAttendanceHistoryData(string? studentId, string? classId, string? month, string? status)
        {
            try
            {
                var parent = await GetCurrentParentAsync();

                if (parent == null)
                {
                    return Json(new { success = false, message = "Unauthorized" });
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
                    return Json(new
                    {
                        success = true,
                        data = new
                        {
                            attendances = new List<object>(),
                            totalPresent = 0,
                            totalAbsent = 0,
                            totalLate = 0,
                            attendanceRate = 0,
                            hasRecords = false
                        }
                    });
                }

                // If studentId specified, filter to that student, otherwise show all
                List<Student> filteredStudents;
                if (!string.IsNullOrEmpty(studentId))
                {
                    filteredStudents = students.Where(s => s.StudentId == studentId).ToList();
                }
                else
                {
                    filteredStudents = students;
                }

                // Get student IDs from filtered list
                var studentIds = filteredStudents.Select(s => s.StudentId).ToList();

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
                }

                if (!string.IsNullOrEmpty(month))
                {
                    if (DateTime.TryParse(month + "-01", out DateTime monthDate))
                    {
                        var startDate = new DateTime(monthDate.Year, monthDate.Month, 1);
                        var endDate = startDate.AddMonths(1).AddDays(-1);
                        query = query.Where(a => a.Date >= startDate && a.Date <= endDate);
                    }
                }

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(a => a.Status == status);
                }

                // Get filtered attendances
                var attendances = await query
                    .OrderByDescending(a => a.Date)
                    .Take(50)
                    .ToListAsync();

                // Calculate statistics
                var totalPresent = attendances.Count(a => a.Status == "Present");
                var totalAbsent = attendances.Count(a => a.Status == "Absent");
                var totalLate = attendances.Count(a => a.Status == "Leave");
                var totalCount = attendances.Count;
                var attendanceRate = totalCount > 0 ? Math.Round((decimal)totalPresent / totalCount * 100, 1) : 0;

                // Map attendances to anonymous objects for JSON
                var attendanceData = attendances.Select(a => new
                {
                    studentName = a.Student.User.FullName,
                    date = a.Date.ToString("yyyy-MM-dd"),
                    day = a.Date.ToString("dddd"),
                    className = a.Class.ClassName,
                    subject = a.Class.Subject?.SubjectName ?? "N/A",
                    timeTaken = a.TakenOn.ToString("hh:mm tt"),
                    status = a.Status,
                    markedBy = a.MarkedByTeacher?.User?.FullName ?? "System"
                }).ToList();

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        attendances = attendanceData,
                        totalPresent = totalPresent,
                        totalAbsent = totalAbsent,
                        totalLate = totalLate,
                        attendanceRate = attendanceRate,
                        hasRecords = attendances.Any(),
                        recordCount = attendances.Count,
                        hasFilters = !string.IsNullOrEmpty(classId) || !string.IsNullOrEmpty(month) || !string.IsNullOrEmpty(status)
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Attendance - View History
        public async Task<IActionResult> AttendanceHistory(string? studentId, string? classId, string? month, string? status)
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

            // Store all students for dropdown
      ViewBag.AllStudents = students;

       // If studentId specified, filter to that student, otherwise show all
          List<Student> filteredStudents;
            if (!string.IsNullOrEmpty(studentId))
    {
     filteredStudents = students.Where(s => s.StudentId == studentId).ToList();
      ViewBag.SelectedStudentId = studentId;
}
        else
    {
        filteredStudents = students;
            }
    
    // Get all enrolled classes for filter dropdown (from filtered students)
 var enrolledClasses = filteredStudents
  .SelectMany(s => s.Enrollments.Select(e => e.Class))
   .Distinct()
  .ToList();
    
    // Get student IDs from filtered list
   var studentIds = filteredStudents.Select(s => s.StudentId).ToList();
  
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
 var totalLate = attendances.Count(a => a.Status == "Leave");
            var totalCount = attendances.Count;
    var attendanceRate = totalCount > 0 ? Math.Round((decimal)totalPresent / totalCount * 100, 1) : 0;
     
         ViewBag.Students = filteredStudents;
    ViewBag.Attendances = attendances;
       ViewBag.Classes = enrolledClasses;
   ViewBag.TotalPresent = totalPresent;
  ViewBag.TotalAbsent = totalAbsent;
    ViewBag.TotalLate = totalLate;
    ViewBag.AttendanceRate = attendanceRate;
 
     return View();
        }

        // Attendance - Monthly Summary
        public async Task<IActionResult> MonthlySummary(string? studentId, int? year, int? month)
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
 ViewBag.AllStudents = new List<Student>();
   return View();
}

     // Store all students for dropdown
     ViewBag.AllStudents = students;

       // If studentId specified, filter to that student, otherwise show all
    List<Student> filteredStudents;
    if (!string.IsNullOrEmpty(studentId))
 {
       filteredStudents = students.Where(s => s.StudentId == studentId).ToList();
      ViewBag.SelectedStudentId = studentId;
       }
else
{
       filteredStudents = students;
       }
     
        // Get student IDs from filtered list
     var studentIds = filteredStudents.Select(s => s.StudentId).ToList();
  
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
var totalLate = monthlyAttendances.Count(a => a.Status == "Leave");
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
    Late = g.Count(a => a.Status == "Leave"),
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
        public async Task<IActionResult> StudentProfile(string? studentId)
        {
      ViewBag.ActiveMenu = "StudentProfile";
  
    var parent = await GetCurrentParentAsync();
   
    if (parent == null || !parent.Students.Any())
    {
        ViewBag.Student = null;
        ViewBag.Parent = parent;
   ViewBag.AllStudents = new List<Student>();
        return View();
    }

    // Get all students for the dropdown selector
    ViewBag.AllStudents = parent.Students.ToList();

// If studentId is not provided, use the first student
    Student? student = null;
    if (!string.IsNullOrEmpty(studentId))
 {
      student = parent.Students.FirstOrDefault(s => s.StudentId == studentId);
    }
      
  // If still null, default to first student
    if (student == null)
 {
        student = parent.Students.FirstOrDefault();
    }

    ViewBag.Student = student;
    ViewBag.Parent = parent;
    ViewBag.ClassPage = 1; // Initialize class page
     
    if (student != null)
    {
        // Calculate attendance statistics
     var allAttendances = student.Attendances;
        var totalAttendance = allAttendances.Count;
     var presentCount = allAttendances.Count(a => a.Status == "Present");
        var absentCount = allAttendances.Count(a => a.Status == "Absent");
     var leaveCount = allAttendances.Count(a => a.Status == "Leave");
        var attendanceRate = totalAttendance > 0 ? Math.Round((decimal)presentCount / totalAttendance * 100, 1) : 0;
       
  ViewBag.TotalAttendance = totalAttendance;
    ViewBag.PresentCount = presentCount;
 ViewBag.AbsentCount = absentCount;
        ViewBag.LeaveCount = leaveCount;
     ViewBag.AttendanceRate = attendanceRate;
    }
    
    return View();
}

// AJAX endpoint to get paginated enrolled classes
[HttpGet]
public async Task<IActionResult> GetEnrolledClasses(string studentId, int page = 1)
{
    try
    {
        var parent = await GetCurrentParentAsync();
   
        if (parent == null)
        {
     return Json(new { success = false, message = "Parent not found" });
   }

        // Find the student
        var student = await _context.Students
            .Include(s => s.Enrollments)
      .ThenInclude(e => e.Class)
       .ThenInclude(c => c.Teacher)
             .ThenInclude(t => t.User)
         .FirstOrDefaultAsync(s => s.StudentId == studentId && s.ParentId == parent.ParentId);

        if (student == null)
        {
        return Json(new { success = false, message = "Student not found" });
        }

 // Pagination settings
        int pageSize = 2;
        var allEnrollments = student.Enrollments.ToList();
        var totalClasses = allEnrollments.Count;
 var totalPages = (int)Math.Ceiling((double)totalClasses / pageSize);
        
        // Ensure page is within valid range
        if (page < 1) page = 1;
        if (page > totalPages && totalPages > 0) page = totalPages;
      
        // Get paginated enrollments
        var pagedEnrollments = allEnrollments
  .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new
  {
     className = e.Class.ClassName,
    day = e.Class.Day,
           startTime = e.Class.StartTime?.ToString("hh\\:mm"),
       endTime = e.Class.EndTime?.ToString("hh\\:mm"),
       teacherName = e.Class.Teacher?.User.FullName
            })
       .ToList();

        return Json(new
        {
         success = true,
        enrollments = pagedEnrollments,
            currentPage = page,
            totalPages = totalPages,
      totalClasses = totalClasses
        });
    }
    catch (Exception ex)
    {
     return Json(new { success = false, message = ex.Message });
    }
}

// AJAX endpoint to get student profile content
[HttpGet]
public async Task<IActionResult> GetStudentProfileContent(string studentId)
{
    var parent = await GetCurrentParentAsync();
   
    if (parent == null || !parent.Students.Any())
    {
        return Json(new { success = false, message = "No students found" });
  }

    // Get all students
    var allStudents = parent.Students.ToList();
    
    // Find the requested student
    var student = allStudents.FirstOrDefault(s => s.StudentId == studentId);
    
    if (student == null)
    {
        return Json(new { success = false, message = "Student not found" });
    }

    // Get current student index for navigation
    var currentIndex = allStudents.FindIndex(s => s.StudentId == studentId);
    var totalChildren = allStudents.Count;
    var hasPrevious = currentIndex > 0;
    var hasNext = currentIndex < totalChildren - 1;
    var previousStudentId = hasPrevious ? allStudents[currentIndex - 1].StudentId : null;
    var nextStudentId = hasNext ? allStudents[currentIndex + 1].StudentId : null;

    // Calculate attendance statistics
    var allAttendances = student.Attendances;
    var totalAttendance = allAttendances.Count;
    var presentCount = allAttendances.Count(a => a.Status == "Present");
  var absentCount = allAttendances.Count(a => a.Status == "Absent");
 var leaveCount = allAttendances.Count(a => a.Status == "Leave");
    var attendanceRate = totalAttendance > 0 ? Math.Round((decimal)presentCount / totalAttendance * 100, 1) : 0;
    
        ViewBag.Student = student;
    ViewBag.TotalAttendance = totalAttendance;
    ViewBag.PresentCount = presentCount;
 ViewBag.AbsentCount = absentCount;
        ViewBag.LeaveCount = leaveCount;
     ViewBag.AttendanceRate = attendanceRate;
    ViewBag.ClassPage = 1; // Initialize class page for new student

    // Render partial view to string
    var htmlContent = await this.RenderViewAsync("_StudentProfileContent", student, true);

    return Json(new
{
        success = true,
        html = htmlContent,
     navigation = new
        {
        studentName = student.User.FullName,
            currentIndex = currentIndex + 1,
            totalChildren = totalChildren,
   hasPrevious = hasPrevious,
 hasNext = hasNext,
    previousStudentId = previousStudentId,
  nextStudentId = nextStudentId
        }
  });
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

        // Delete notification
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteNotification(string notificationId)
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

                _context.Notifications.Remove(notification);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Delete all read notifications
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAllRead()
        {
            try
            {
                var parent = await GetCurrentParentAsync();

                if (parent == null)
                {
                    return Json(new { success = false, message = "Unauthorized" });
                }

                var notifications = await _context.Notifications
                    .Where(n => n.UserId == parent.UserId && n.Status == "read")
                    .ToListAsync();

                _context.Notifications.RemoveRange(notifications);
                await _context.SaveChangesAsync();

                return Json(new { success = true, count = notifications.Count });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Get notification details
        public async Task<IActionResult> GetNotificationDetails(string notificationId)
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

                // Build detailed data based on notification type - adapted for parent perspective
                object detailData = null;

                switch (notification.Type)
                {
                    case "Student Enrollment":
                        if (!string.IsNullOrEmpty(notification.RelatedEntityId))
                        {
                            var classInfo = await _context.Classes
                                .Include(c => c.Enrollments)
                                    .ThenInclude(e => e.Student)
                                        .ThenInclude(s => s.User)
                                .Include(c => c.Teacher)
                                    .ThenInclude(t => t.User)
                                .Include(c => c.Subject)
                                .FirstOrDefaultAsync(c => c.ClassId == notification.RelatedEntityId);

                            if (classInfo != null)
                            {
                                // Get the affected student (parent's child)
                                var affectedStudentIds = notification.AffectedEntityId?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? new string[0];
                                
                                var enrolledStudents = classInfo.Enrollments
                                    .Where(e => affectedStudentIds.Contains(e.Student.StudentId) && e.UnenrolledDate == null)
                                    .Select(e => new
                                    {
                                        studentId = e.Student.StudentId,
                                        studentName = e.Student.User.FullName,
                                        email = e.Student.User.Email,
                                        enrolledDate = e.EnrolledDate.ToString("dd MMM yyyy")
                                    }).ToList();

                                detailData = new
                                {
                                    teacherName = classInfo.Teacher?.User?.FullName,
                                    teacherGender = classInfo.Teacher?.User?.Gender,
                                    className = classInfo.ClassName,
                                    totalEnrolled = classInfo.CurrentCapacity,
                                    capacity = classInfo.MaxCapacity,
                                    day = classInfo.Day,
                                    startTime = classInfo.StartTime?.ToString(@"hh\:mm"),
                                    endTime = classInfo.EndTime?.ToString(@"hh\:mm"),
                                    room = classInfo.RoomNumber,
                                    subject = classInfo.Subject?.SubjectName,
                                    students = enrolledStudents
                                };
                            }
                        }
                        break;

                    case "Student Unenrollment":
                        if (!string.IsNullOrEmpty(notification.RelatedEntityId))
                        {
                            var classInfo = await _context.Classes
                                .Include(c => c.Enrollments)
                                    .ThenInclude(e => e.Student)
                                        .ThenInclude(s => s.User)
                                .Include(c => c.Teacher)
                                    .ThenInclude(t => t.User)
                                .Include(c => c.Subject)
                                .FirstOrDefaultAsync(c => c.ClassId == notification.RelatedEntityId);

                            if (classInfo != null)
                            {
                                // Get the affected student (parent's child) - including unenrolled
                                var affectedStudentIds = notification.AffectedEntityId?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? new string[0];
                                
                                var unenrolledStudents = classInfo.Enrollments
                                    .Where(e => affectedStudentIds.Contains(e.Student.StudentId) && e.UnenrolledDate != null)
                                    .Select(e => new
                                    {
                                        studentId = e.Student.StudentId,
                                        studentName = e.Student.User.FullName,
                                        email = e.Student.User.Email,
                                        enrolledDate = e.EnrolledDate.ToString("dd MMM yyyy"),
                                        unenrolledDate = e.UnenrolledDate.HasValue ? e.UnenrolledDate.Value.ToString("dd MMM yyyy") : "N/A"
                                    }).ToList();

                                detailData = new
                                {
                                    teacherName = classInfo.Teacher?.User?.FullName,
                                    teacherGender = classInfo.Teacher?.User?.Gender,
                                    className = classInfo.ClassName,
                                    totalEnrolled = classInfo.CurrentCapacity,
                                    capacity = classInfo.MaxCapacity,
                                    day = classInfo.Day,
                                    startTime = classInfo.StartTime?.ToString(@"hh\:mm"),
                                    endTime = classInfo.EndTime?.ToString(@"hh\:mm"),
                                    room = classInfo.RoomNumber,
                                    subject = classInfo.Subject?.SubjectName,
                                    students = unenrolledStudents
                                };
                            }
                        }
                        break;

                    case "Leave Approved":
                    case "Leave Rejected":
                    case "Leave Application":
                        if (!string.IsNullOrEmpty(notification.RelatedEntityId))
                        {
                            var leave = await _context.LeaveApplications
                                .Include(l => l.User)
                                .FirstOrDefaultAsync(l => l.LeaveId == notification.RelatedEntityId);

                            if (leave != null)
                            {
                                detailData = new
                                {
                                    leaveId = leave.LeaveId,
                                    applicant = leave.User.FullName,
                                    email = leave.User.Email,
                                    startDate = leave.StartDate.ToString("dd MMM yyyy"),
                                    endDate = leave.EndDate.ToString("dd MMM yyyy"),
                                    totalDays = leave.TotalDays,
                                    reason = leave.Reason,
                                    status = leave.Status,
                                    createdDate = leave.CreatedDate.ToString("dd MMM yyyy hh:mm tt"),
                                    remarks = leave.Remarks
                                };
                            }
                        }
                        break;

                    case "Attendance Marked":
                    case "Low Attendance Alert":
                    case "Low Attendance Warning":
                    case "Absent Alert":
                        if (!string.IsNullOrEmpty(notification.RelatedEntityId))
                        {
                            var student = await _context.Students
                                .Include(s => s.User)
                                .Include(s => s.Attendances)
                                    .ThenInclude(a => a.Class)
                                .FirstOrDefaultAsync(s => s.StudentId == notification.RelatedEntityId);

                            if (student != null)
                            {
                                var totalClasses = student.Attendances.Count();
                                var presentCount = student.Attendances.Count(a => a.Status == "Present" || a.Status == "Leave");
                                var absentCount = student.Attendances.Count(a => a.Status == "Absent");
                                var attendanceRate = totalClasses > 0 ? (presentCount * 100.0 / totalClasses) : 0;

                                detailData = new
                                {
                                    studentId = student.StudentId,
                                    studentName = student.User.FullName,
                                    email = student.User.Email,
                                    totalClasses = totalClasses,
                                    presentCount = presentCount,
                                    absentCount = absentCount,
                                    attendanceRate = $"{attendanceRate:F1}%"
                                };
                            }
                        }
                        break;

                    case "Class Schedule Update":
                    case "Class Information":
                        if (!string.IsNullOrEmpty(notification.RelatedEntityId))
                        {
                            var classInfo = await _context.Classes
                                .Include(c => c.Teacher)
                                    .ThenInclude(t => t.User)
                                .Include(c => c.Subject)
                                .FirstOrDefaultAsync(c => c.ClassId == notification.RelatedEntityId);

                            if (classInfo != null)
                            {
                                detailData = new
                                {
                                    className = classInfo.ClassName,
                                    teacher = classInfo.Teacher?.User?.FullName,
                                    room = classInfo.RoomNumber,
                                    day = classInfo.Day,
                                    time = classInfo.StartTime != null && classInfo.EndTime != null 
                                        ? $"{classInfo.StartTime:hh\\:mm} - {classInfo.EndTime:hh\\:mm}"
                                        : "Not set",
                                    capacity = $"{classInfo.CurrentCapacity}/{classInfo.MaxCapacity}",
                                    subject = classInfo.Subject?.SubjectName
                                };
                            }
                        }
                        break;
                }

                return Json(new
                {
                    success = true,
                    notification = new
                    {
                        id = notification.NotificationId,
                        type = notification.Type,
                        description = notification.Description,
                        status = notification.Status,
                        createdDate = notification.CreatedDate.ToString("dd MMM yyyy hh:mm tt")
                    },
                    details = detailData
                });
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
            ViewBag.ActiveSubmenu = "AccountSettings";
        
            var parent = await GetCurrentParentAsync();
            ViewBag.Parent = parent;
            return View();
        }

        // Change Password Page
        public async Task<IActionResult> ChangePassword()
        {
 ViewBag.ActiveMenu = "Settings";
            ViewBag.ActiveSubmenu = "ChangePassword";
        
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

        // Change Password POST
        [HttpPost]
        [ValidateAntiForgeryToken]
 public async Task<IActionResult> ChangePasswordPost(string currentPassword, string newPassword, string confirmPassword)
        {
  try
 {
var parent = await GetCurrentParentAsync();

    if (parent == null)
   {
   TempData["Error"] = "Parent profile not found.";
        return RedirectToAction("ChangePassword");
    }

   // Verify new password and confirm password match
      if (newPassword != confirmPassword)
    {
          TempData["Error"] = "New password and confirm password do not match.";
  return RedirectToAction("ChangePassword");
    }

   // Validate password strength using Helper method
    var (isValid, errors) = _helper.ValidatePasswordStrength(newPassword);
    if (!isValid)
    {
        TempData["Error"] = "Password does not meet security requirements:<br/>" + string.Join("<br/>", errors);
        return RedirectToAction("ChangePassword");
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
       return RedirectToAction("ChangePassword");
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
        
     return RedirectToAction("ChangePassword");
        }

        // Download Report - Optional PDF generation functionality
   public IActionResult DownloadReport()
  {
            // TODO: Implement PDF generation logic in the future if needed
   // For now, redirect back to Monthly Summary
   TempData["Info"] = "PDF download feature will be available soon.";
 return RedirectToAction("MonthlySummary");
        }

        // Export Monthly Summary as PDF
        public async Task<IActionResult> ExportPdf(string? studentId, int? year, int? month)
        {
            try
            {
                var parent = await GetCurrentParentAsync();

                if (parent == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                // Get the same data as MonthlySummary
                var selectedYear = year ?? DateTime.Now.Year;
                var selectedMonth = month ?? DateTime.Now.Month;
                var startDate = new DateTime(selectedYear, selectedMonth, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

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
                    TempData["Error"] = "No student data found.";
                    return RedirectToAction("MonthlySummary");
                }

                List<Student> filteredStudents;
                if (!string.IsNullOrEmpty(studentId))
                {
                    filteredStudents = students.Where(s => s.StudentId == studentId).ToList();
                }
                else
                {
                    filteredStudents = students;
                }

                var studentIds = filteredStudents.Select(s => s.StudentId).ToList();

                var monthlyAttendances = await _context.Attendances
                    .Include(a => a.Class)
                    .ThenInclude(c => c.Subject)
                    .Where(a => studentIds.Contains(a.StudentId) &&
                                a.Date >= startDate &&
                                a.Date <= endDate)
                    .ToListAsync();

                var totalClasses = monthlyAttendances.Count;
                var totalPresent = monthlyAttendances.Count(a => a.Status == "Present");
                var totalAbsent = monthlyAttendances.Count(a => a.Status == "Absent");
                var totalLate = monthlyAttendances.Count(a => a.Status == "Leave");
                var attendanceRate = totalClasses > 0 ? Math.Round((decimal)totalPresent / totalClasses * 100, 1) : 0;

                var subjectSummary = monthlyAttendances
                    .GroupBy(a => new
                    {
                        SubjectId = a.Class.SubjectId,
                        SubjectName = a.Class.Subject?.SubjectName ?? "N/A"
                    })
                    .Select(g => new ReportService.SubjectSummary
                    {
                        Subject = g.Key.SubjectName,
                        TotalClasses = g.Count(),
                        Present = g.Count(a => a.Status == "Present"),
                        Absent = g.Count(a => a.Status == "Absent"),
                        Late = g.Count(a => a.Status == "Leave"),
                        AttendanceRate = g.Count() > 0 ? Math.Round((decimal)g.Count(a => a.Status == "Present") / g.Count() * 100, 1) : 0
                    })
                    .OrderBy(s => s.Subject)
                    .ToList();

                var reportData = new ReportService.AttendanceSummaryData
                {
                    MonthName = startDate.ToString("MMMM yyyy"),
                    StudentName = filteredStudents.Count == 1 ? filteredStudents.First().User.FullName : "All Children",
                    TotalClasses = totalClasses,
                    TotalPresent = totalPresent,
                    TotalAbsent = totalAbsent,
                    TotalLate = totalLate,
                    AttendanceRate = attendanceRate,
                    SubjectSummaries = subjectSummary
                };

                var pdfBytes = _reportService.GeneratePdfReport(reportData);
                var fileName = $"Attendance_Summary_{startDate:yyyy_MM}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error generating PDF: {ex.Message}";
                return RedirectToAction("MonthlySummary");
            }
        }

        // Export Monthly Summary as Excel
        public async Task<IActionResult> ExportExcel(string? studentId, int? year, int? month)
        {
            try
            {
                var parent = await GetCurrentParentAsync();

                if (parent == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                // Get the same data as MonthlySummary
                var selectedYear = year ?? DateTime.Now.Year;
                var selectedMonth = month ?? DateTime.Now.Month;
                var startDate = new DateTime(selectedYear, selectedMonth, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

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
                    TempData["Error"] = "No student data found.";
                    return RedirectToAction("MonthlySummary");
                }

                List<Student> filteredStudents;
                if (!string.IsNullOrEmpty(studentId))
                {
                    filteredStudents = students.Where(s => s.StudentId == studentId).ToList();
                }
                else
                {
                    filteredStudents = students;
                }

                var studentIds = filteredStudents.Select(s => s.StudentId).ToList();

                var monthlyAttendances = await _context.Attendances
                    .Include(a => a.Class)
                    .ThenInclude(c => c.Subject)
                    .Where(a => studentIds.Contains(a.StudentId) &&
                                a.Date >= startDate &&
                                a.Date <= endDate)
                    .ToListAsync();

                var totalClasses = monthlyAttendances.Count;
                var totalPresent = monthlyAttendances.Count(a => a.Status == "Present");
                var totalAbsent = monthlyAttendances.Count(a => a.Status == "Absent");
                var totalLate = monthlyAttendances.Count(a => a.Status == "Leave");
                var attendanceRate = totalClasses > 0 ? Math.Round((decimal)totalPresent / totalClasses * 100, 1) : 0;

                var subjectSummary = monthlyAttendances
                    .GroupBy(a => new
                    {
                        SubjectId = a.Class.SubjectId,
                        SubjectName = a.Class.Subject?.SubjectName ?? "N/A"
                    })
                    .Select(g => new ReportService.SubjectSummary
                    {
                        Subject = g.Key.SubjectName,
                        TotalClasses = g.Count(),
                        Present = g.Count(a => a.Status == "Present"),
                        Absent = g.Count(a => a.Status == "Absent"),
                        Late = g.Count(a => a.Status == "Leave"),
                        AttendanceRate = g.Count() > 0 ? Math.Round((decimal)g.Count(a => a.Status == "Present") / g.Count() * 100, 1) : 0
                    })
                    .OrderBy(s => s.Subject)
                    .ToList();

                var reportData = new ReportService.AttendanceSummaryData
                {
                    MonthName = startDate.ToString("MMMM yyyy"),
                    StudentName = filteredStudents.Count == 1 ? filteredStudents.First().User.FullName : "All Children",
                    TotalClasses = totalClasses,
                    TotalPresent = totalPresent,
                    TotalAbsent = totalAbsent,
                    TotalLate = totalLate,
                    AttendanceRate = attendanceRate,
                    SubjectSummaries = subjectSummary
                };

                var excelBytes = _reportService.GenerateExcelReport(reportData);
                var fileName = $"Attendance_Summary_{startDate:yyyy_MM}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error generating Excel: {ex.Message}";
                return RedirectToAction("MonthlySummary");
            }
        }

        // ==================== PROFILE PICTURE UPLOAD ENDPOINTS ====================

        [HttpPost]
      public async Task<IActionResult> UploadProfilePicture(IFormFile file, string userId)
      {
            try
            {
    Console.WriteLine($"[UploadProfilePicture] Starting upload for userId: {userId}");
  
          if (file == null || file.Length == 0)
              {
   Console.WriteLine("[UploadProfilePicture] ERROR: No file uploaded");
            return Json(new { success = false, message = "No file uploaded" });
            }

          // Validate file type
          var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
     var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (!allowedExtensions.Contains(extension))
         {
       Console.WriteLine($"[UploadProfilePicture] ERROR: Invalid file type: {extension}");
           return Json(new { success = false, message = "Invalid file type. Only JPG, PNG, GIF, and WEBP are allowed." });
      }

          // Validate file size (5 MB)
      if (file.Length > 5 * 1024 * 1024)
       {
    Console.WriteLine($"[UploadProfilePicture] ERROR: File too large: {file.Length} bytes");
        return Json(new { success = false, message = "File size must not exceed 5 MB" });
        }

            // Get current parent
              Console.WriteLine("[UploadProfilePicture] Getting current parent...");
      var parent = await GetCurrentParentAsync();
     
      if (parent == null)
     {
        Console.WriteLine("[UploadProfilePicture] ERROR: Parent not found");
          return Json(new { success = false, message = "Parent profile not found. Please log in again." });
    }
            
     if (parent.User == null)
    {
        Console.WriteLine("[UploadProfilePicture] ERROR: Parent.User is null");
        return Json(new { success = false, message = "User information not found. Please log in again." });
}
                
      Console.WriteLine($"[UploadProfilePicture] Parent found: {parent.ParentId}, User: {parent.User.UserId}");
       
          if (parent.User.UserId != userId)
          {
           Console.WriteLine($"[UploadProfilePicture] ERROR: Unauthorized - Expected: {userId}, Got: {parent.User.UserId}");
        return Json(new { success = false, message = "Unauthorized access" });
          }

            // Delete old profile picture if exists
              if (!string.IsNullOrEmpty(parent.User.ProfilePicture) && 
    !parent.User.ProfilePicture.StartsWith("/images/"))
          {
     Console.WriteLine($"[UploadProfilePicture] Deleting old profile picture: {parent.User.ProfilePicture}");
     try
       {
         await _s3Service.DeleteFileAsync(parent.User.ProfilePicture);
            Console.WriteLine("[UploadProfilePicture] Old picture deleted successfully");
        }
           catch (Exception ex)
   {
          Console.WriteLine($"[UploadProfilePicture] Warning: Failed to delete old profile picture: {ex.Message}");
              }
    }

  // Upload to S3
  Console.WriteLine("[UploadProfilePicture] Uploading to S3...");
  var s3Url = await _s3Service.UploadFileAsync(file, userId);
   Console.WriteLine($"[UploadProfilePicture] Upload successful: {s3Url}");

     // Update user profile picture
     parent.User.ProfilePicture = s3Url;
       _context.Update(parent.User);
                await _context.SaveChangesAsync();
 
           Console.WriteLine("[UploadProfilePicture] Database updated successfully");

     return Json(new { success = true, message = "Profile picture uploaded successfully!", url = s3Url });
  }
 catch (Exception ex)
     {
                Console.WriteLine($"[UploadProfilePicture] ERROR: {ex.GetType().Name} - {ex.Message}");
   Console.WriteLine($"[UploadProfilePicture] Stack trace: {ex.StackTrace}");
                return Json(new { success = false, message = $"Upload failed: {ex.Message}" });
      }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteProfilePicture([FromBody] DeleteProfilePictureRequest request)
        {
            try
            {
                Console.WriteLine($"[DeleteProfilePicture] Starting delete for userId: {request?.UserId}");
  
            if (string.IsNullOrEmpty(request?.UserId))
     {
             Console.WriteLine("[DeleteProfilePicture] ERROR: UserId is required");
   return Json(new { success = false, message = "User ID is required" });
          }

                // Get current parent
                Console.WriteLine("[DeleteProfilePicture] Getting current parent...");
        var parent = await GetCurrentParentAsync();
       
      if (parent == null)
     {
     Console.WriteLine("[DeleteProfilePicture] ERROR: Parent not found");
  return Json(new { success = false, message = "Parent profile not found. Please log in again." });
     }
    
     if (parent.User == null)
    {
     Console.WriteLine("[DeleteProfilePicture] ERROR: Parent.User is null");
    return Json(new { success = false, message = "User information not found. Please log in again." });
            }
    
      Console.WriteLine($"[DeleteProfilePicture] Parent found: {parent.ParentId}, User: {parent.User.UserId}");
           
        if (parent.User.UserId != request.UserId)
 {
           Console.WriteLine($"[DeleteProfilePicture] ERROR: Unauthorized - Expected: {request.UserId}, Got: {parent.User.UserId}");
   return Json(new { success = false, message = "Unauthorized access" });
  }

         // Delete from S3 if exists
       if (!string.IsNullOrEmpty(parent.User.ProfilePicture) && 
    !parent.User.ProfilePicture.StartsWith("/images/"))
                {
            Console.WriteLine($"[DeleteProfilePicture] Deleting from S3: {parent.User.ProfilePicture}");
        try
        {
            await _s3Service.DeleteFileAsync(parent.User.ProfilePicture);
    Console.WriteLine("[DeleteProfilePicture] S3 deletion successful");
   }
         catch (Exception ex)
        {
               Console.WriteLine($"[DeleteProfilePicture] Warning: Failed to delete from S3: {ex.Message}");
             }
       }

          // Update user profile picture to default
    parent.User.ProfilePicture = "/images/default-avatar.png";
     _context.Update(parent.User);
                await _context.SaveChangesAsync();
      
         Console.WriteLine("[DeleteProfilePicture] Database updated successfully");

                return Json(new { success = true, message = "Profile picture removed successfully!" });
 }
            catch (Exception ex)
{
           Console.WriteLine($"[DeleteProfilePicture] ERROR: {ex.GetType().Name} - {ex.Message}");
      Console.WriteLine($"[DeleteProfilePicture] Stack trace: {ex.StackTrace}");
    return Json(new { success = false, message = $"Delete failed: {ex.Message}" });
      }
        }

        public class DeleteProfilePictureRequest
    {
            public string UserId { get; set; }
        }
    }
}
