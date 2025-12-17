using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebMobileAssignment.Models;

namespace WebMobileAssignment.Controllers
{
    [Authorize(Roles = "Teacher")]
    public class TeacherController : Controller
    {
        private readonly DB _db;
        private readonly Helper _helper;

        public TeacherController(DB db, Helper helper)
        {
            _db = db;
            _helper = helper;
        }

        // Helper method to get current teacher and set ViewBag
        private async Task<Teacher> GetCurrentTeacherAsync()
        {
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(userEmail))
                return null;

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
            if (user == null)
                return null;

            var teacher = await _db.Teachers
                .Include(t => t.User)
                .Include(t => t.Classes)
                .FirstOrDefaultAsync(t => t.UserId == user.UserId);

            if (teacher != null)
                ViewBag.Teacher = teacher;

            return teacher;
        }

        // Dashboard
        public async Task<IActionResult> TeachDashboard()
        {
            ViewBag.ActiveMenu = "Dashboard";

            // Get current user from claims (email stored during login)
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
            
            if (string.IsNullOrEmpty(userEmail))
            {
                return RedirectToAction("Login", "Account");
            }

            // Get user from database and then get teacher info
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Email == userEmail);

            if (user == null || user.UserType.ToLower() != "teacher")
            {
                return Unauthorized();
            }

            // Get teacher information
            var teacher = await _db.Teachers
                .Include(t => t.User)
                .Include(t => t.Classes)
                    .ThenInclude(c => c.Enrollments)
                .Include(t => t.Classes)
                    .ThenInclude(c => c.Attendances)
                .FirstOrDefaultAsync(t => t.UserId == user.UserId);

            if (teacher == null)
            {
                ViewBag.ErrorMessage = "Teacher profile not found.";
                return View("TeachDashboard");
            }

            // Calculate statistics
            var totalClasses = teacher.Classes.Count;
            var totalStudents = teacher.Classes.SelectMany(c => c.Enrollments).Count();
            var today = DateTime.Today;
            var todaysAttendance = teacher.Classes
                .SelectMany(c => c.Attendances)
                .Where(a => a.Date.Date == today)
                .ToList();

            var attendanceRate = todaysAttendance.Count > 0
                ? (decimal)(todaysAttendance.Count(a => a.Status == "Present") * 100) / todaysAttendance.Count()
                : 0;

            ViewBag.TotalClasses = totalClasses;
            ViewBag.TotalStudents = totalStudents;
            ViewBag.AttendanceRate = Math.Round(attendanceRate, 2);
            ViewBag.SessionsCount = teacher.Classes.SelectMany(c => c.Attendances).Count();
            ViewBag.Classes = teacher.Classes.ToList();
            ViewBag.RecentAttendances = todaysAttendance.OrderByDescending(a => a.TakenOn).Take(5).ToList();
            ViewBag.Teacher = teacher;

            return View("TeachDashboard");
        }

        // Attendance - Mark Attendance
        public async Task<IActionResult> TeachMarkAttendance(string selectedDate = null)
        {
            ViewBag.ActiveMenu = "Attendance";
            ViewBag.ActiveSubmenu = "MarkAttendance";
            
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(userEmail))
                return RedirectToAction("Login", "Account");
            
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
            if (user == null)
                return Unauthorized();
            
            var teacherId = await _db.Teachers
                .Where(t => t.UserId == user.UserId)
                .Select(t => t.TeacherId)
                .FirstOrDefaultAsync();
            
            // Default to today's date if not provided
            DateTime dateToCheck = string.IsNullOrEmpty(selectedDate) 
                ? DateTime.Now 
                : DateTime.Parse(selectedDate);
            
            // Get the day of week (Monday, Tuesday, etc.)
            string dayOfWeek = dateToCheck.ToString("dddd");
            
            // Get all classes for today's day of week
            var classes = await _db.Classes
                .Include(c => c.Enrollments)
                    .ThenInclude(e => e.Student)
                        .ThenInclude(s => s.User)
                .Include(c => c.Subject)
                .Where(c => c.TeacherId == teacherId && c.Day == dayOfWeek)
                .OrderBy(c => c.StartTime)
                .ToListAsync();
            
            ViewBag.Classes = classes;
            ViewBag.SelectedDate = dateToCheck.ToString("yyyy-MM-dd");
            ViewBag.DayOfWeek = dayOfWeek;
            await GetCurrentTeacherAsync();
            return View("TeachMarkAttendance");
        }

        // Attendance - View History
        public async Task<IActionResult> TeachAttendanceHistory()
        {
            ViewBag.ActiveMenu = "Attendance";
            ViewBag.ActiveSubmenu = "History";
            await GetCurrentTeacherAsync();
            
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(userEmail))
                return RedirectToAction("Login", "Account");
            
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
            if (user == null)
                return Unauthorized();
            
            var attendances = await _db.Attendances
                .Include(a => a.Student)
                    .ThenInclude(s => s.User)
                .Include(a => a.Class)
                .Include(a => a.MarkedByTeacher)
                    .ThenInclude(t => t.User)
                .Where(a => a.MarkedByTeacher != null && a.MarkedByTeacher.User.UserId == user.UserId)
                .OrderByDescending(a => a.Date)
                .ToListAsync();
            
            ViewBag.Attendances = attendances;
            return View("TeachAttendanceHistory");
        }

        // Classes
        public async Task<IActionResult> TeachClasses()
        {
            ViewBag.ActiveMenu = "Classes";
            
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(userEmail))
                return RedirectToAction("Login", "Account");
            
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
            if (user == null)
                return Unauthorized();
            
            var classes = await _db.Classes
                .Include(c => c.Teacher)
                    .ThenInclude(t => t.User)
                .Include(c => c.Enrollments)
                .Include(c => c.Attendances)
                .Include(c => c.Subject)
                .Where(c => c.Teacher != null && c.Teacher.User.UserId == user.UserId)
                .ToListAsync();
            
            ViewBag.Classes = classes;
            await GetCurrentTeacherAsync();
            return View("TeachClasses");
        }

        // Class Detail
        public async Task<IActionResult> TeachClassDetail(string id)
        {
            ViewBag.ActiveMenu = "Classes";
            await GetCurrentTeacherAsync();
            
            var classDetail = await _db.Classes
                .Include(c => c.Teacher)
                    .ThenInclude(t => t.User)
                .Include(c => c.Enrollments)
                    .ThenInclude(e => e.Student)
                        .ThenInclude(s => s.User)
                .Include(c => c.Attendances)
                .Include(c => c.Subject)
                .FirstOrDefaultAsync(c => c.ClassId == id);
            
            if (classDetail == null)
                return NotFound();
            
            // Calculate attendance statistics per student
            var enrollmentCount = classDetail.Enrollments?.Count ?? 0;
            ViewBag.TotalStudents = enrollmentCount;
            
            var attendanceStats = new List<dynamic>();
            if (enrollmentCount > 0)
            {
                foreach (var enrollment in classDetail.Enrollments)
                {
                    var studentAttendances = classDetail.Attendances
                        .Where(a => a.StudentId == enrollment.StudentId)
                        .ToList();
                    
                    var present = studentAttendances.Count(a => a.Status == "Present");
                    var absent = studentAttendances.Count(a => a.Status == "Absent");
                    var late = studentAttendances.Count(a => a.Status == "Late");
                    var total = studentAttendances.Count;
                    var rate = total > 0 ? Math.Round((decimal)(present * 100) / total, 2) : 0;
                    
                    attendanceStats.Add(new
                    {
                        StudentId = enrollment.StudentId,
                        StudentName = enrollment.Student?.User?.FullName,
                        Present = present,
                        Absent = absent,
                        Late = late,
                        Total = total,
                        Rate = rate
                    });
                }
            }
            
            ViewBag.AttendanceStats = attendanceStats;
            return View("TeachClassDetail", classDetail);
        }

        // Students
        public async Task<IActionResult> TeachStudents()
        {
            ViewBag.ActiveMenu = "Students";
            await GetCurrentTeacherAsync();
            
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(userEmail))
                return RedirectToAction("Login", "Account");
            
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
            if (user == null)
                return Unauthorized();
            
            var teacher = await _db.Teachers
                .FirstOrDefaultAsync(t => t.UserId == user.UserId);
            
            if (teacher == null)
            {
                ViewBag.Students = new List<Student>();
                return View("TeachStudents");
            }
            
            var students = await _db.Students
                .Include(s => s.User)
                .Include(s => s.Parent)
                    .ThenInclude(p => p.User)
                .Where(s => s.Enrollments.Any(e => e.Class.TeacherId == teacher.TeacherId))
                .OrderBy(s => s.User.FullName)
                .ToListAsync();
            
            ViewBag.Students = students;
            return View("TeachStudents");
        }

        // Teacher Profile
        public async Task<IActionResult> TeachProfile()
        {
            ViewBag.ActiveMenu = "Profile";
            
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(userEmail))
                return RedirectToAction("Login", "Account");
            
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
            if (user == null)
                return Unauthorized();
            
            var teacher = await _db.Teachers
                .Include(t => t.User)
                .Include(t => t.Classes)
                .FirstOrDefaultAsync(t => t.UserId == user.UserId);
            
            if (teacher == null)
                return NotFound();
            
            await GetCurrentTeacherAsync();
            return View("TeachProfile", teacher);
        }

        // Settings
        public async Task<IActionResult> TeachSettings()
        {
            ViewBag.ActiveMenu = "Settings";
            await GetCurrentTeacherAsync();
            return View("TeachSettings");
        }

        // Change Password
        public async Task<IActionResult> TeachChangePassword()
        {
            ViewBag.ActiveMenu = "Settings";
            ViewBag.ActiveSubmenu = "ChangePassword";
            await GetCurrentTeacherAsync();
            return View("TeachChangePassword");
        }

        [HttpPost]
        public async Task<IActionResult> TeachChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            ViewBag.ActiveMenu = "Settings";
            ViewBag.ActiveSubmenu = "ChangePassword";
            await GetCurrentTeacherAsync();

            if (newPassword != confirmPassword)
            {
                ViewBag.Error = "New password and confirm password do not match.";
                return View("TeachChangePassword");
            }

            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(userEmail))
                return RedirectToAction("Login", "Account");

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
            if (user == null)
                return Unauthorized();

            // Verify current password
            if (!_helper.VerifyPassword(user.PasswordHash, currentPassword))
            {
                ViewBag.Error = "Current password is incorrect.";
                return View("TeachChangePassword");
            }

            // Update password
            user.PasswordHash = _helper.HashPassword(newPassword);
            _db.Users.Update(user);
            await _db.SaveChangesAsync();

            ViewBag.Success = "Password changed successfully.";
            return View("TeachChangePassword");
        }
    }
}
