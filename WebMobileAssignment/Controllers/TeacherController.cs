using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebMobileAssignment.Models;
using WebMobileAssignment.Services;

namespace WebMobileAssignment.Controllers
{
    [Authorize(Roles = "Teacher")]
    public class TeacherController : Controller
    {
        private readonly DB _db;
        private readonly Helper _helper;
        private readonly S3Service _s3Service;

        public TeacherController(DB db, Helper helper, S3Service s3Service)
        {
            _db = db;
            _helper = helper;
            _s3Service = s3Service;
        }

        // Helper method to get current teacher and set ViewBag
        private async Task<Teacher?> GetCurrentTeacherAsync()
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

            // Count sessions created by this teacher (not all attendance records)
            var teacherId = teacher.TeacherId;
            var totalSessions = await _db.AttendanceSessions
                .CountAsync(s => s.CreatedByTeacherId == teacherId);

            // Get recent sessions for display
            var recentSessions = await _db.AttendanceSessions
                .Include(s => s.Class)
                .Where(s => s.CreatedByTeacherId == teacherId)
                .OrderByDescending(s => s.CreatedDate)
                .Take(10)
                .ToListAsync();

            // Get sessions created by this teacher for each class (same logic as TeachClasses)
            var sessionCounts = new Dictionary<string, int>();
            foreach (var cls in teacher.Classes)
            {
                var count = await _db.AttendanceSessions
                    .CountAsync(s => s.ClassId == cls.ClassId && s.CreatedByTeacherId == teacherId);
                sessionCounts[cls.ClassId] = count;
            }

            ViewBag.TotalClasses = totalClasses;
            ViewBag.TotalStudents = totalStudents;
            ViewBag.AttendanceRate = Math.Round(attendanceRate, 2);
            ViewBag.SessionsCount = totalSessions;
            ViewBag.Classes = teacher.Classes.ToList();
            ViewBag.SessionCounts = sessionCounts;
            ViewBag.RecentSessions = recentSessions;
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
                ? DateTime.Today 
                : DateTime.Parse(selectedDate);
            
            // Get the day of week (Monday, Tuesday, etc.)
            string dayOfWeek = dateToCheck.DayOfWeek.ToString();
            
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
            ViewBag.SelectedDate = dateToCheck;
            ViewBag.SelectedDay = dayOfWeek;
            
            // Load ONLY sessions created on the selected date
            ViewBag.Sessions = await _db.AttendanceSessions
                .Include(s => s.Class)
                .Where(s => s.IsActive && s.CreatedDate.Date == dateToCheck.Date && 
                           classes.Select(c => c.ClassId).Contains(s.ClassId))
                .ToListAsync();
            
            await GetCurrentTeacherAsync();
            return View("TeachMarkAttendance");
        }

        // Attendance - Class Detail
        public async Task<IActionResult> TeachAttendanceClassDetail(string id, string selectedDate = null)
        {
            ViewBag.ActiveMenu = "Attendance";
            ViewBag.ActiveSubmenu = "MarkAttendance";
            
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(userEmail))
                return RedirectToAction("Login", "Account");
            
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
            if (user == null)
                return Unauthorized();
            
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
            
            // Verify teacher owns this class
            if (classDetail.Teacher?.User?.UserId != user.UserId)
                return Unauthorized();
            
            // Set selected date
            DateTime dateToCheck = string.IsNullOrEmpty(selectedDate) 
                ? DateTime.Today 
                : DateTime.Parse(selectedDate);
            
            ViewBag.SelectedDate = dateToCheck;
            
            var selectedDay = dateToCheck.DayOfWeek.ToString();
            var isSelectedDate = !string.IsNullOrEmpty(classDetail.Day) && classDetail.Day.Equals(selectedDay, StringComparison.OrdinalIgnoreCase);
            
            ViewBag.WasActiveOnDate = isSelectedDate;
            
            // Load the session created on the selected date for this class
            var sessionOnSelectedDate = await _db.AttendanceSessions
                .FirstOrDefaultAsync(s => s.IsActive && 
                           s.ClassId == id && 
                           s.CreatedDate.Date == dateToCheck.Date);
            
            // Filter enrollments to show students who:
            // 1. Were enrolled on or before the selected date, AND
            // 2. Either still enrolled (UnenrolledDate == null) OR
            // 3. Were unenrolled on a different date OR
            // 4. Were unenrolled on the same date but AFTER the class started
            var classStartDateTime = classDetail.StartTime.HasValue 
                ? dateToCheck.Date.Add(classDetail.StartTime.Value) 
                : dateToCheck.Date;
            
            var filteredEnrollments = classDetail.Enrollments
                .Where(e => e.EnrolledDate.Date <= dateToCheck.Date && 
                           (e.UnenrolledDate == null || 
                            e.UnenrolledDate.Value.Date > dateToCheck.Date ||
                            (e.UnenrolledDate.Value.Date == dateToCheck.Date && e.UnenrolledDate.Value > classStartDateTime)))
                .ToList();
            
            ViewBag.FilteredEnrollments = filteredEnrollments;
            
            // Load ONLY the session created on the selected date for this class
            ViewBag.Sessions = await _db.AttendanceSessions
                .Where(s => s.IsActive && 
                           s.ClassId == id && 
                           s.CreatedDate.Date == dateToCheck.Date)
                .ToListAsync();
            
            // Load ALL sessions for this class (to show if there's a PIN from a different date)
            ViewBag.AllSessions = await _db.AttendanceSessions
                .Where(s => s.IsActive && s.ClassId == id)
                .OrderByDescending(s => s.CreatedDate)
                .ToListAsync();
            
            // Load attendance records for this class on the selected date
            var todayAttendances = await _db.Attendances
                .Where(a => a.ClassId == id && a.Date.Date == dateToCheck.Date)
                .ToListAsync();
            
            ViewBag.TodayAttendances = todayAttendances;
            
            await GetCurrentTeacherAsync();
            return View("TeachAttendanceClassDetail", classDetail);
        }

        // Generate Attendance PIN
        [HttpPost]
        public async Task<IActionResult> GenerateAttendancePin(string classId)
        {
            try
            {
                if (string.IsNullOrEmpty(classId))
                {
                    return Json(new { success = false, message = "Class ID is required" });
                }

                // Get the class
                var classEntity = await _db.Classes
                    .Include(c => c.Teacher)
                    .FirstOrDefaultAsync(c => c.ClassId == classId);

                if (classEntity == null)
                {
                    return Json(new { success = false, message = "Class not found" });
                }

                // Verify teacher owns this class
                var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
                if (classEntity.Teacher?.User?.UserId != user?.UserId)
                {
                    return Json(new { success = false, message = "Unauthorized" });
                }

                // Check if class has a schedule
                if (!classEntity.StartTime.HasValue || !classEntity.EndTime.HasValue)
                {
                    return Json(new { success = false, message = "Class schedule not configured" });
                }

                // Check if today matches the class day
                var todayDay = DateTime.Now.DayOfWeek.ToString();
                if (!string.IsNullOrEmpty(classEntity.Day) && !classEntity.Day.Equals(todayDay, StringComparison.OrdinalIgnoreCase))
                {
                    return Json(new { success = false, message = $"PIN can only be generated on {classEntity.Day}, not {todayDay}" });
                }

                // Check if current time is within class hours
                var currentTime = DateTime.Now.TimeOfDay;
                if (currentTime < classEntity.StartTime.Value || currentTime > classEntity.EndTime.Value)
                {
                    return Json(new { success = false, message = $"PIN can only be generated during class hours ({classEntity.StartTime.Value:hh\\:mm} - {classEntity.EndTime.Value:hh\\:mm})" });
                }

                // Check if PIN already exists for today
                var existingSession = await _db.AttendanceSessions
                    .FirstOrDefaultAsync(s => s.ClassId == classId && 
                                            s.IsActive && 
                                            s.CreatedDate.Date == DateTime.Today);

                if (existingSession != null)
                {
                    return Json(new { success = false, message = "PIN has already been generated for this class today" });
                }

                // Generate random 6-digit PIN
                var pinCode = IdGenerator.GenerateAttendancePinCode(_db);

                // Create attendance session
                var sessionId = IdGenerator.GenerateSessionId(_db);

                var session = new AttendanceSession
                {
                    SessionId = sessionId,
                    PinCode = pinCode,
                    ClassId = classId,
                    CreatedByTeacherId = classEntity.TeacherId,
                    CreatedDate = DateTime.Now,
                    ExpiryDate = DateTime.Today.Add(classEntity.EndTime.Value), // Expires at class end time
                    IsActive = true,
                    SessionType = "Class"
                };

                _db.AttendanceSessions.Add(session);
                await _db.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    pinCode = pinCode,
                    sessionId = sessionId,
                    expiryDate = session.ExpiryDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    message = "PIN generated successfully"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating PIN: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return Json(new { success = false, message = $"Error generating PIN: {ex.Message}" });
            }
        }

        // Save single attendance
        [HttpPost]
        [HttpPost]
        public async Task<IActionResult> SaveTeacherAttendance([FromBody] TeacherAttendanceRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.StudentId) || string.IsNullOrEmpty(request.ClassId))
                    return Json(new { success = false, message = "Invalid request data" });
                
                if (!DateTime.TryParse(request.Date, out DateTime date))
                    return Json(new { success = false, message = "Invalid date" });
                
                var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
                
                if (user == null)
                    return Unauthorized();
                
                var teacher = await _db.Teachers
                    .FirstOrDefaultAsync(t => t.UserId == user.UserId);
                
                if (teacher == null)
                    return Unauthorized();
                
                // Verify teacher owns this class
                var classObj = await _db.Classes
                    .FirstOrDefaultAsync(c => c.ClassId == request.ClassId && c.TeacherId == teacher.TeacherId);
                
                if (classObj == null)
                    return Unauthorized();
                
                // Find or create attendance record
                var attendance = await _db.Attendances
                    .FirstOrDefaultAsync(a => a.StudentId == request.StudentId && 
                                              a.ClassId == request.ClassId && 
                                              a.Date.Date == date.Date);
                
                // Prevent changing attendance if student is on Leave
                if (attendance != null && attendance.Status == "Leave")
                    return Json(new { success = false, message = "Cannot change attendance for student on leave" });
                
                if (attendance == null)
                {
                    attendance = new Attendance
                    {
                        StudentId = request.StudentId,
                        ClassId = request.ClassId,
                        Status = request.Status,
                        Date = date,
                        MarkedByTeacherId = teacher.TeacherId,
                        TakenOn = DateTime.Now
                    };
                    _db.Attendances.Add(attendance);
                }
                else
                {
                    attendance.Status = request.Status;
                    attendance.MarkedByTeacherId = teacher.TeacherId;
                    attendance.TakenOn = DateTime.Now;
                    _db.Attendances.Update(attendance);
                }
                
                await _db.SaveChangesAsync();
                
                // Send notifications for absent attendance
                if (request.Status == "Absent")
                {
                    await SendTeacherAttendanceNotifications(request.StudentId, request.ClassId, date, "Absent");
                }
                
                return Json(new { success = true, message = "Attendance saved successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Save batch attendance
        [HttpPost]
        public async Task<IActionResult> SaveTeacherManualAttendance([FromBody] TeacherManualAttendanceRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.ClassId) || request.Attendances == null || !request.Attendances.Any())
                {
                    return Json(new { success = false, message = "Invalid request data" });
                }

                var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
                
                if (user == null)
                    return Unauthorized();
                
                var teacher = await _db.Teachers
                    .FirstOrDefaultAsync(t => t.UserId == user.UserId);
                
                if (teacher == null)
                    return Unauthorized();
                
                // Verify teacher owns this class
                var classObj = await _db.Classes
                    .FirstOrDefaultAsync(c => c.ClassId == request.ClassId && c.TeacherId == teacher.TeacherId);
                
                if (classObj == null)
                    return Json(new { success = false, message = "Class not found or unauthorized" });

                var selectedDate = DateTime.Parse(request.Date);
                int markedCount = 0;

                // Get the current maximum attendance count ONCE outside the loop
                var currentAttendanceCount = await _db.Attendances.CountAsync();

                foreach (var att in request.Attendances)
                {
                    // Skip "Not Marked" status - don't save it to database
                    if (att.Status == "Not Marked")
                    {
                        continue;
                    }

                    // Find or create attendance record
                    var attendance = await _db.Attendances
                        .FirstOrDefaultAsync(a => a.StudentId == att.StudentId && 
                                                  a.ClassId == request.ClassId && 
                                                  a.Date.Date == selectedDate.Date);
                    
                    // Skip if student is on Leave - cannot change leave status
                    if (attendance != null && attendance.Status == "Leave")
                    {
                        continue;
                    }
                    
                    if (attendance == null)
                    {
                        currentAttendanceCount++;
                        var attId = $"ATT{(currentAttendanceCount + 1):D5}";

                        attendance = new Attendance
                        {
                            AttendanceId = attId,
                            StudentId = att.StudentId,
                            ClassId = request.ClassId,
                            Status = att.Status,
                            Date = selectedDate,
                            MarkedByTeacherId = teacher.TeacherId,
                            TakenOn = DateTime.Now
                        };
                        _db.Attendances.Add(attendance);
                        markedCount++;
                    }
                    else
                    {
                        if (attendance.Status != att.Status)
                        {
                            attendance.Status = att.Status;
                            attendance.MarkedByTeacherId = teacher.TeacherId;
                            attendance.TakenOn = DateTime.Now;
                            _db.Attendances.Update(attendance);
                            markedCount++;
                        }
                    }
                }
                
                await _db.SaveChangesAsync();
                return Json(new { 
                    success = true, 
                    marked = markedCount,
                    message = $"Successfully saved attendance for {markedCount} student(s)" 
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
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
            
            var teacher = await _db.Teachers
                .FirstOrDefaultAsync(t => t.UserId == user.UserId);
            
            if (teacher == null)
                return Unauthorized();
            
            // Get all classes for this teacher
            var teacherClasses = await _db.Classes
                .Where(c => c.TeacherId == teacher.TeacherId)
                .ToListAsync();
            
            var teacherClassIds = teacherClasses.Select(c => c.ClassId).ToList();
            
            // Debug: Log teacher and class info
            Console.WriteLine($"Teacher ID: {teacher.TeacherId}");
            Console.WriteLine($"Number of classes: {teacherClasses.Count}");
            Console.WriteLine($"Class IDs: {string.Join(", ", teacherClassIds)}");
            
            // Get attendance records marked by this teacher only
            var attendanceRecords = await _db.Attendances
                .Include(a => a.Class)
                .Include(a => a.Student)
                    .ThenInclude(s => s.User)
                .Where(a => a.MarkedByTeacherId == teacher.TeacherId)
                .OrderByDescending(a => a.Date)
                .ThenByDescending(a => a.TakenOn)
                .ToListAsync();
            
            // Debug: Log attendance info
            Console.WriteLine($"Total attendance records found: {attendanceRecords.Count}");
            
            // Map to view model
            var attendanceList = attendanceRecords
                .Select(a => new
                {
                    AttendanceId = a.AttendanceId,
                    Date = a.Date,
                    StudentId = a.StudentId,
                    StudentName = a.Student?.User?.FullName ?? "Unknown",
                    ClassId = a.ClassId,
                    ClassName = a.Class?.ClassName ?? "Unknown",
                    RoomNumber = a.Class?.RoomNumber,
                    Status = a.Status,
                    TakenOn = a.TakenOn,
                    MarkedByTeacherId = a.MarkedByTeacherId
                })
                .ToList();
            
            Console.WriteLine($"Attendance list count: {attendanceList.Count}");
            
            ViewBag.Attendances = attendanceList;
            return View("TeachAttendanceHistory");
        }
        

        // ==================== NOTIFICATIONS ====================
        public async Task<IActionResult> TeachNotifications()
        {
            ViewBag.ActiveMenu = "Notifications";
            ViewBag.Title = "Notifications";

            // Get current teacher user
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
            var teacher = await _db.Users
                .FirstOrDefaultAsync(u => u.Email == userEmail && u.UserType == "Teacher");

            if (teacher == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Get notifications for this teacher user
            var notifications = await _db.Notifications
                .Include(n => n.User)
                .Where(n => n.UserId == teacher.UserId)
                .OrderByDescending(n => n.CreatedDate)
                .ToListAsync();

            // Calculate notification stats
            var totalNotifications = notifications.Count;
            var unreadCount = notifications.Count(n => n.Status == "unread");
            var readCount = notifications.Count(n => n.Status == "read");

            // Count leave application notifications
            var leaveCount = notifications.Count(n =>
                n.Description.ToLower().Contains("leave application") &&
                n.Status == "unread");

            ViewBag.TotalNotifications = totalNotifications;
            ViewBag.UnreadCount = unreadCount;
            ViewBag.ReadCount = readCount;
            ViewBag.LeaveCount = leaveCount;
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
                var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
                var teacher = await _db.Users
                    .FirstOrDefaultAsync(u => u.Email == userEmail && u.UserType == "Teacher");

                if (teacher == null)
                {
                    return Json(new { success = false, message = "Unauthorized" });
                }

                var notification = await _db.Notifications
                    .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == teacher.UserId);

                if (notification == null)
                {
                    return Json(new { success = false, message = "Notification not found" });
                }

                notification.Status = "read";
                await _db.SaveChangesAsync();

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
                var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
                var teacher = await _db.Users
                    .FirstOrDefaultAsync(u => u.Email == userEmail && u.UserType == "Teacher");

                if (teacher == null)
                {
                    return Json(new { success = false, message = "Unauthorized" });
                }

                var notifications = await _db.Notifications
                    .Where(n => n.UserId == teacher.UserId && n.Status == "unread")
                    .ToListAsync();

                foreach (var notification in notifications)
                {
                    notification.Status = "read";
                }

                await _db.SaveChangesAsync();

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
                var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
                var teacher = await _db.Users
                    .FirstOrDefaultAsync(u => u.Email == userEmail && u.UserType == "Teacher");

                if (teacher == null)
                {
                    return Json(new { success = false, message = "Unauthorized" });
                }

                var notification = await _db.Notifications
                    .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == teacher.UserId);

                if (notification == null)
                {
                    return Json(new { success = false, message = "Notification not found" });
                }

                _db.Notifications.Remove(notification);
                await _db.SaveChangesAsync();

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
                var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
                var teacher = await _db.Users
                    .FirstOrDefaultAsync(u => u.Email == userEmail && u.UserType == "Teacher");

                if (teacher == null)
                {
                    return Json(new { success = false, message = "Unauthorized" });
                }

                var notifications = await _db.Notifications
                    .Where(n => n.UserId == teacher.UserId && n.Status == "read")
                    .ToListAsync();

                _db.Notifications.RemoveRange(notifications);
                await _db.SaveChangesAsync();

                return Json(new { success = true, count = notifications.Count });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }


        // Get unread notification count (for layout badge)
        public async Task<IActionResult> GetUnreadNotificationCount()
        {
            try
            {
                var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
                var teacher = await _db.Users
                    .FirstOrDefaultAsync(u => u.Email == userEmail && u.UserType == "Teacher");

                if (teacher == null)
                {
                    return Json(new { success = false, count = 0 });
                }

                var unreadCount = await _db.Notifications
                    .CountAsync(n => n.UserId == teacher.UserId && n.Status == "unread");

                return Json(new { success = true, count = unreadCount });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, count = 0, message = ex.Message });
            }
        }

        // Get notification details
        public async Task<IActionResult> GetNotificationDetails(string notificationId)
        {
            try
            {
                var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
                var teacher = await _db.Users
                    .FirstOrDefaultAsync(u => u.Email == userEmail && u.UserType == "Teacher");

                if (teacher == null)
                {
                    return Json(new { success = false, message = "Unauthorized" });
                }

                var notification = await _db.Notifications
                    .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == teacher.UserId);

                if (notification == null)
                {
                    return Json(new { success = false, message = "Notification not found" });
                }

                object details = null;

                // Build details based on notification type and RelatedEntityId
                if (!string.IsNullOrEmpty(notification.RelatedEntityId))
                {
                    switch (notification.Type)
                    {
                        case "Class Assignment":
                            var assignedClass = await _db.Classes
                                .Include(c => c.Teacher)
                                    .ThenInclude(t => t.User)
                                .Include(c => c.Subject)
                                .FirstOrDefaultAsync(c => c.ClassId == notification.RelatedEntityId);
                            if (assignedClass != null)
                            {
                                details = new
                                {
                                    className = assignedClass.ClassName,
                                    teacher = assignedClass.Teacher?.User?.FullName,
                                    room = assignedClass.RoomNumber,
                                    day = assignedClass.Day,
                                    time = $"{assignedClass.StartTime} - {assignedClass.EndTime}",
                                    capacity = $"{assignedClass.CurrentCapacity} / {assignedClass.MaxCapacity}",
                                    subject = assignedClass.Subject?.SubjectName
                                };
                            }
                            break;

                        case "Student Enrollment":
                        case "Student Unenrollment":
                            var enrollClass = await _db.Classes
                                .Include(c => c.Enrollments)
                                    .ThenInclude(e => e.Student)
                                    .ThenInclude(s => s.User)
                                .Include(c => c.Teacher)
                                    .ThenInclude(t => t.User)
                                .Include(c => c.Subject)
                                .FirstOrDefaultAsync(c => c.ClassId == notification.RelatedEntityId);
                            if (enrollClass != null)
                            {
                                // Split the comma-separated student IDs
                                var affectedStudentIds = notification.AffectedEntityId?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? new string[0];
                                
                                var students = enrollClass.Enrollments
                                    .Where(e => affectedStudentIds.Contains(e.Student.StudentId))
                                    .Select(e => new
                                    {
                                        studentId = e.Student.StudentId,
                                        studentName = e.Student.User.FullName,
                                        email = e.Student.User.Email,
                                        enrolledDate = e.EnrolledDate.ToString("dd MMM yyyy")
                                    }).ToList();

                                details = new
                                {
                                    teacherName = enrollClass.Teacher?.User?.FullName,
                                    teacherGender = enrollClass.Teacher?.User?.Gender,
                                    className = enrollClass.ClassName,
                                    totalEnrolled = enrollClass.CurrentCapacity,
                                    capacity = enrollClass.MaxCapacity,
                                    day = enrollClass.Day,
                                    startTime = enrollClass.StartTime?.ToString(@"hh\:mm"),
                                    endTime = enrollClass.EndTime?.ToString(@"hh\:mm"),
                                    room = enrollClass.RoomNumber,
                                    subject = enrollClass.Subject?.SubjectName,
                                    students
                                };
                            }
                            else
                            {
                                // Debug: class not found
                                details = new { error = "Class not found with ID: " + notification.RelatedEntityId };
                            }
                            break;

                        case "Leave Application":
                        case "Student Leave Application":
                        case "Leave Approved":
                        case "Leave Rejected":
                            var leave = await _db.LeaveApplications
                                .Include(l => l.User)
                                .FirstOrDefaultAsync(l => l.LeaveId == notification.RelatedEntityId);
                            if (leave != null)
                            {
                                details = new
                                {
                                    leaveId = leave.LeaveId,
                                    applicant = leave.User.FullName,
                                    email = leave.User.Email,
                                    startDate = leave.StartDate.ToString("dd MMM yyyy"),
                                    endDate = leave.EndDate.ToString("dd MMM yyyy"),
                                    totalDays = (leave.EndDate - leave.StartDate).Days + 1,
                                    reason = leave.Reason,
                                    status = leave.Status,
                                    remarks = leave.Remarks,
                                    createdDate = leave.CreatedDate.ToString("dd MMM yyyy HH:mm")
                                };
                            }
                            break;

                        case "Student Registration":
                        case "Teacher Registration":
                        case "Parent Registration":
                            var registeredUser = await _db.Users
                                .FirstOrDefaultAsync(u => u.UserId == notification.RelatedEntityId);
                            if (registeredUser != null)
                            {
                                details = new
                                {
                                    userId = registeredUser.UserId,
                                    fullName = registeredUser.FullName,
                                    email = registeredUser.Email,
                                    phoneNumber = registeredUser.PhoneNumber,
                                    userType = registeredUser.UserType,
                                    status = registeredUser.Status,
                                    createdDate = registeredUser.CreatedDate.ToString("dd MMM yyyy HH:mm")
                                };
                            }
                            break;

                        case "Attendance Marked":
                        case "Low Attendance Alert":
                        case "Low Attendance Warning":
                            var student = await _db.Students
                                .Include(s => s.User)
                                .Include(s => s.Attendances)
                                .FirstOrDefaultAsync(s => s.StudentId == notification.RelatedEntityId);
                            if (student != null)
                            {
                                var totalClasses = student.Attendances.Count();
                                var presentCount = student.Attendances.Count(a => a.Status == "Present");
                                var absentCount = totalClasses - presentCount;
                                var attendanceRate = totalClasses > 0 ? ((double)presentCount / totalClasses * 100).ToString("F1") + "%" : "N/A";

                                details = new
                                {
                                    studentId = student.StudentId,
                                    studentName = student.User.FullName,
                                    email = student.User.Email,
                                    totalClasses,
                                    presentCount,
                                    absentCount,
                                    attendanceRate
                                };
                            }
                            break;

                        case "Class Capacity Alert":
                            var capacityClass = await _db.Classes
                                .Include(c => c.Teacher)
                                    .ThenInclude(t => t.User)
                                .FirstOrDefaultAsync(c => c.ClassId == notification.RelatedEntityId);
                            if (capacityClass != null)
                            {
                                var utilizationRate = capacityClass.MaxCapacity > 0
                                    ? ((double)capacityClass.CurrentCapacity / capacityClass.MaxCapacity * 100).ToString("F1") + "%"
                                    : "N/A";

                                details = new
                                {
                                    className = capacityClass.ClassName,
                                    teacher = capacityClass.Teacher?.User?.FullName,
                                    room = capacityClass.RoomNumber,
                                    currentEnrollment = capacityClass.CurrentCapacity,
                                    maxCapacity = capacityClass.MaxCapacity,
                                    utilizationRate
                                };
                            }
                            break;
                    }
                }
                else
                {
                    // Debug: RelatedEntityId is null or empty
                    details = new { error = "RelatedEntityId is null or empty", notificationType = notification.Type };
                }

                return Json(new
                {
                    success = true,
                    notification = new
                    {
                        type = notification.Type,
                        description = notification.Description,
                        createdDate = notification.CreatedDate.ToString("dd MMM yyyy HH:mm")
                    },
                    details
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
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

            var teacher = await _db.Teachers
                .FirstOrDefaultAsync(t => t.UserId == user.UserId);
            
            if (teacher == null)
                return Unauthorized();
            
            var classes = await _db.Classes
                .Include(c => c.Teacher)
                    .ThenInclude(t => t.User)
                .Include(c => c.Enrollments)
                .Include(c => c.Attendances)
                .Include(c => c.Subject)
                .Where(c => c.Teacher != null && c.Teacher.User.UserId == user.UserId)
                .ToListAsync();

            // Get sessions created by this teacher for each class
            var sessionCounts = new Dictionary<string, int>();
            foreach (var cls in classes)
            {
                var count = await _db.AttendanceSessions
                    .CountAsync(s => s.ClassId == cls.ClassId && s.CreatedByTeacherId == teacher.TeacherId);
                sessionCounts[cls.ClassId] = count;
            }
            
            ViewBag.SessionCounts = sessionCounts;
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

            // Get the current teacher
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(userEmail))
                return Unauthorized();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
            if (user == null)
                return Unauthorized();

            var teacher = await _db.Teachers.FirstOrDefaultAsync(t => t.UserId == user.UserId);
            if (teacher == null)
                return Unauthorized();
            
            // Get all sessions created by this teacher for this class
            var teacherId = teacher.TeacherId;
            var sessions = await _db.AttendanceSessions
                .Where(s => s.ClassId == id && s.CreatedByTeacherId == teacherId)
                .OrderByDescending(s => s.CreatedDate)
                .ToListAsync();
            
            ViewBag.Sessions = sessions;
            ViewBag.SessionCount = sessions.Count;
            
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
                    var leave = studentAttendances.Count(a => a.Status == "Leave");
                    var total = studentAttendances.Count;
                    var rate = total > 0 ? Math.Round((decimal)(present * 100) / total, 2) : 0;
                    
                    attendanceStats.Add(new
                    {
                        StudentId = enrollment.StudentId,
                        StudentName = enrollment.Student?.User?.FullName,
                        Present = present,
                        Absent = absent,
                        Leave = leave,
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
                .Include(s => s.Enrollments)
                    .ThenInclude(e => e.Class)
                        .ThenInclude(c => c.Teacher)
                            .ThenInclude(t => t.User)
                .Include(s => s.Enrollments)
                    .ThenInclude(e => e.Class)
                        .ThenInclude(c => c.Attendances)
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
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null)
                return RedirectToAction("Index", "Home");
            return View("TeachSettings", teacher);
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

        // Upload Profile Picture to AWS S3
        [HttpPost]
        public async Task<IActionResult> UploadProfilePicture(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return Json(new { success = false, message = "No file selected." });
                }

                // Validate file type
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                {
                    return Json(new { success = false, message = "Invalid file type. Only JPG, PNG, GIF, and WEBP are allowed." });
                }

                // Validate file size (5MB max)
                if (file.Length > 5 * 1024 * 1024)
                {
                    return Json(new { success = false, message = "File size must not exceed 5MB." });
                }

                // Get current user
                var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(userEmail))
                {
                    return Json(new { success = false, message = "User not found." });
                }

                var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                // Delete old profile picture from S3 if exists
                if (!string.IsNullOrEmpty(user.ProfilePicture) && !user.ProfilePicture.StartsWith("/images/"))
                {
                    try
                    {
                        await _s3Service.DeleteFileAsync(user.ProfilePicture);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Failed to delete old profile picture: {ex.Message}");
                    }
                }

                // Upload to AWS S3
                var s3Url = await _s3Service.UploadFileAsync(file, user.UserId);

                // Update user profile picture URL
                user.ProfilePicture = s3Url;
                _db.Users.Update(user);
                await _db.SaveChangesAsync();

                return Json(new { success = true, message = "Profile picture uploaded successfully!", pictureUrl = s3Url });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Upload failed: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateTeacherProfile(string fullName, string phoneNumber, DateTime? dateOfBirth, string gender, string title)
        {
            try
            {
                var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(userEmail))
                {
                    return RedirectToAction("TeachProfile");
                }

                var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
                if (user == null)
                {
                    return RedirectToAction("TeachProfile");
                }

                // Update user information
                user.FullName = fullName;
                user.PhoneNumber = phoneNumber;
                user.DateOfBirth = dateOfBirth;
                user.Gender = gender;

                // Update teacher title
                var teacher = await _db.Teachers.FirstOrDefaultAsync(t => t.UserId == user.UserId);
                if (teacher != null)
                {
                    teacher.Title = title;
                    _db.Teachers.Update(teacher);
                }

                _db.Users.Update(user);
                await _db.SaveChangesAsync();

                TempData["Success"] = "Profile updated successfully!";
                return RedirectToAction("TeachProfile");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"An error occurred: {ex.Message}";
                return RedirectToAction("TeachProfile");
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteProfilePicture()
        {
            try
            {
                var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(userEmail))
                {
                    return Json(new { success = false, message = "User not found." });
                }

                var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                // Delete the profile picture file
                if (!string.IsNullOrEmpty(user.ProfilePicture) && !user.ProfilePicture.StartsWith("/images/"))
                {
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", user.ProfilePicture.TrimStart('/'));
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }

                // Clear profile picture URL
                user.ProfilePicture = null;
                _db.Users.Update(user);
                await _db.SaveChangesAsync();

                return Json(new { success = true, message = "Profile picture removed successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }
        
        // Helper method to send attendance notifications
        private async Task SendTeacherAttendanceNotifications(string studentId, string classId, DateTime date, string status)
        {
            try
            {
                var student = await _db.Students
                    .Include(s => s.User)
                    .Include(s => s.Parent)
                    .ThenInclude(p => p.User)
                    .FirstOrDefaultAsync(s => s.StudentId == studentId);
                
                var classEntity = await _db.Classes.FirstOrDefaultAsync(c => c.ClassId == classId);
                
                if (student == null || classEntity == null) return;
                
                // Notify student about absent attendance
                var studentNotificationId = IdGenerator.GenerateNotificationId(_db);
                var studentNotification = new Notification
                {
                    NotificationId = studentNotificationId,
                    UserId = student.UserId,
                    Type = "Attendance Marked",
                    Description = $"You were marked {status} for {classEntity.ClassName} on {date:dd MMM yyyy}",
                    Status = "unread",
                    CreatedDate = DateTime.Now
                };
                _db.Notifications.Add(studentNotification);
                
                // Notify parent if exists
                if (student.Parent?.User != null)
                {
                    var parentNotificationId = IdGenerator.GenerateNotificationId(_db);
                    var parentNotification = new Notification
                    {
                        NotificationId = parentNotificationId,
                        UserId = student.Parent.UserId,
                        Type = "Child Attendance Alert",
                        Description = $"Your child {student.User.FullName} was marked {status} for {classEntity.ClassName} on {date:dd MMM yyyy}",
                        Status = "unread",
                        CreatedDate = DateTime.Now
                    };
                    _db.Notifications.Add(parentNotification);
                }
                
                // Check for low attendance (below 60%)
                var totalClasses = await _db.Attendances
                    .Where(a => a.StudentId == studentId && a.ClassId == classId)
                    .CountAsync();
                
                if (totalClasses >= 5) // Only check if at least 5 classes
                {
                    var presentCount = await _db.Attendances
                        .Where(a => a.StudentId == studentId && a.ClassId == classId && 
                               (a.Status == "Present" || a.Status == "Late"))
                        .CountAsync();
                    
                    var attendanceRate = (double)presentCount / totalClasses * 100;
                    
                    if (attendanceRate < 60)
                    {
                        // Notify admins
                        var adminUsers = await _db.Users.Where(u => u.UserType == "Admin").ToListAsync();
                        foreach (var admin in adminUsers)
                        {
                            var adminNotificationId = IdGenerator.GenerateNotificationId(_db);
                            var adminNotification = new Notification
                            {
                                NotificationId = adminNotificationId,
                                UserId = admin.UserId,
                                Type = "Low Attendance Alert",
                                Description = $"Student {student.User.FullName} has low attendance in {classEntity.ClassName}: {attendanceRate:F1}% ({presentCount}/{totalClasses})",
                                Status = "unread",
                                CreatedDate = DateTime.Now
                            };
                            _db.Notifications.Add(adminNotification);
                        }
                        
                        // Notify parent
                        if (student.Parent?.User != null)
                        {
                            var parentLowAttendanceId = IdGenerator.GenerateNotificationId(_db);
                            var parentLowAttendance = new Notification
                            {
                                NotificationId = parentLowAttendanceId,
                                UserId = student.Parent.UserId,
                                Type = "Low Attendance Warning",
                                Description = $"Warning: Your child {student.User.FullName} has low attendance in {classEntity.ClassName}: {attendanceRate:F1}% ({presentCount}/{totalClasses} classes attended)",
                                Status = "unread",
                                CreatedDate = DateTime.Now
                            };
                            _db.Notifications.Add(parentLowAttendance);
                        }
                    }
                }
                
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending attendance notifications: {ex.Message}");
            }
        }

        // Request model for teacher manual attendance saving
        public class TeacherAttendanceRequest
        {
            [System.Text.Json.Serialization.JsonPropertyName("classId")]
            public string ClassId { get; set; } = string.Empty;
            
            [System.Text.Json.Serialization.JsonPropertyName("studentId")]
            public string StudentId { get; set; } = string.Empty;
            
            [System.Text.Json.Serialization.JsonPropertyName("date")]
            public string Date { get; set; } = string.Empty;
            
            [System.Text.Json.Serialization.JsonPropertyName("status")]
            public string Status { get; set; } = string.Empty;
        }

        public class TeacherManualAttendanceRequest
        {
            [System.Text.Json.Serialization.JsonPropertyName("classId")]
            public string ClassId { get; set; } = string.Empty;
            
            [System.Text.Json.Serialization.JsonPropertyName("date")]
            public string Date { get; set; } = string.Empty;
            
            [System.Text.Json.Serialization.JsonPropertyName("attendances")]
            public List<TeacherAttendanceItem> Attendances { get; set; } = new List<TeacherAttendanceItem>();
        }

        public class TeacherAttendanceItem
        {
            [System.Text.Json.Serialization.JsonPropertyName("studentId")]
            public string StudentId { get; set; } = string.Empty;
            
            [System.Text.Json.Serialization.JsonPropertyName("status")]
            public string Status { get; set; } = string.Empty;
        }
    }
}
