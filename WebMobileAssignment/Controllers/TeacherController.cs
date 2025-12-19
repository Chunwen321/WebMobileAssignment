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
        public async Task<IActionResult> SaveTeacherAttendance([FromBody] dynamic data)
        {
            try
            {
                var studentId = (string)data.studentId;
                var classId = (string)data.classId;
                var status = (string)data.status;
                var dateStr = (string)data.date;
                
                if (!DateTime.TryParse(dateStr, out DateTime date))
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
                    .FirstOrDefaultAsync(c => c.ClassId == classId && c.TeacherId == teacher.TeacherId);
                
                if (classObj == null)
                    return Unauthorized();
                
                // Find or create attendance record
                var attendance = await _db.Attendances
                    .FirstOrDefaultAsync(a => a.StudentId == studentId && 
                                              a.ClassId == classId && 
                                              a.Date.Date == date.Date);
                
                if (attendance == null)
                {
                    attendance = new Attendance
                    {
                        StudentId = studentId,
                        ClassId = classId,
                        Status = status,
                        Date = date,
                        MarkedByTeacherId = teacher.TeacherId,
                        TakenOn = DateTime.Now
                    };
                    _db.Attendances.Add(attendance);
                }
                else
                {
                    attendance.Status = status;
                    attendance.MarkedByTeacherId = teacher.TeacherId;
                    attendance.TakenOn = DateTime.Now;
                    _db.Attendances.Update(attendance);
                }
                
                await _db.SaveChangesAsync();
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
            var teacherClassIds = await _db.Classes
                .Where(c => c.TeacherId == teacher.TeacherId)
                .Select(c => c.ClassId)
                .ToListAsync();
            
            // Get all sessions created by this teacher for their classes
            var sessions = await _db.AttendanceSessions
                .Include(s => s.Class)
                .Where(s => teacherClassIds.Contains(s.ClassId) && s.CreatedByTeacherId == teacher.TeacherId)
                .OrderByDescending(s => s.CreatedDate)
                .ToListAsync();
            
            // Build session list with attendance rates
            var sessionList = new List<dynamic>();
            
            foreach (var session in sessions)
            {
                var classData = session.Class;
                
                // Get enrollments active at the time of the session was created
                var enrollmentCount = await _db.Enrollments
                    .CountAsync(e => e.ClassId == session.ClassId && 
                                     e.EnrolledDate <= session.CreatedDate &&
                                     (e.UnenrolledDate == null || e.UnenrolledDate > session.CreatedDate));
                
                // Get attendance records for this session (same date as session created)
                var attendances = await _db.Attendances
                    .Where(a => a.ClassId == session.ClassId && 
                               a.Date.Date == session.CreatedDate.Date)
                    .ToListAsync();
                
                var presentCount = attendances.Count(a => a.Status == "Present");
                
                sessionList.Add(new
                {
                    SessionId = session.SessionId,
                    ClassId = session.ClassId,
                    ClassName = classData?.ClassName,
                    RoomNumber = classData?.RoomNumber,
                    StartTime = classData?.StartTime,
                    EndTime = classData?.EndTime,
                    CreatedDate = session.CreatedDate,
                    EnrollmentCount = enrollmentCount,
                    PresentCount = presentCount,
                    AttendanceRate = enrollmentCount > 0 ? Math.Round((decimal)(presentCount * 100) / enrollmentCount, 1) : 0
                });
            }
            
            ViewBag.Sessions = sessionList;
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

        // Upload Profile Picture
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
                var allowedMimeTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
                if (!allowedMimeTypes.Contains(file.ContentType.ToLower()))
                {
                    return Json(new { success = false, message = "Please upload a valid image file (JPEG, PNG, GIF, or WebP)." });
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

                // Create uploads directory if it doesn't exist
                var uploadsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "profiles");
                Directory.CreateDirectory(uploadsDirectory);

                // Generate unique filename
                var fileName = $"{user.UserId}_{Guid.NewGuid()}_{file.FileName}";
                var filePath = Path.Combine(uploadsDirectory, fileName);

                // Save file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Update user profile picture URL
                user.ProfilePicture = $"/uploads/profiles/{fileName}";
                _db.Users.Update(user);
                await _db.SaveChangesAsync();

                return Json(new { success = true, message = "Profile picture uploaded successfully.", pictureUrl = user.ProfilePicture });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
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

        // Request model for teacher manual attendance saving
        public class TeacherManualAttendanceRequest
        {
            public string ClassId { get; set; } = string.Empty;
            public string Date { get; set; } = string.Empty;
            public List<TeacherAttendanceItem> Attendances { get; set; } = new List<TeacherAttendanceItem>();
        }

        public class TeacherAttendanceItem
        {
            public string StudentId { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
        }
    }
}
