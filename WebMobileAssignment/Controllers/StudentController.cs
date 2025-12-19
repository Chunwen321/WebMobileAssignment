using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebMobileAssignment.Models;
using WebMobileAssignment.Services;
using System.Security.Claims;

namespace WebMobileAssignment.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController : Controller
    {
        private readonly DB _context;
        private readonly S3Service _s3Service;
        private readonly Helper _helper;
        private readonly PdfService _pdfService;

        public StudentController(DB context, S3Service s3Service, Helper helper, PdfService pdfService)
        {
            _context = context;
            _s3Service = s3Service;
            _helper = helper;
            _pdfService = pdfService;
        }

        // Helper method to get current student
        private async Task<Student?> GetCurrentStudent()
        {
            // Get email from ClaimTypes.Name (as set by Helper.SignIn)
            var email = User.FindFirstValue(ClaimTypes.Name);
            
            Console.WriteLine($"[GetCurrentStudent] Email from claims: {email ?? "NULL"}");
            Console.WriteLine($"[GetCurrentStudent] User.Identity.IsAuthenticated: {User.Identity?.IsAuthenticated}");
            Console.WriteLine($"[GetCurrentStudent] User.Identity.Name: {User.Identity?.Name ?? "NULL"}");
            
            if (string.IsNullOrEmpty(email))
            {
                Console.WriteLine("[GetCurrentStudent] Email is null or empty - user not authenticated");
                return null;
            }

            // Find student by email (include related Class -> Subject and Teacher->User)
            var student = await _context.Students
                .Include(s => s.User)
                .Include(s => s.Enrollments)
                    .ThenInclude(e => e.Class)
                        .ThenInclude(c => c.Subject)
                .Include(s => s.Enrollments)
                    .ThenInclude(e => e.Class)
                        .ThenInclude(c => c.Teacher)
                            .ThenInclude(t => t.User)
                .FirstOrDefaultAsync(s => s.User.Email == email);

            if (student == null)
            {
                Console.WriteLine($"[GetCurrentStudent] No student found for email: {email}");
            }
            else
            {
                Console.WriteLine($"[GetCurrentStudent] Student found: {student.StudentId} - {student.User.FullName}");
            }

            // expose to layouts/views
            try
            {
                ViewBag.Student = student;
            }
            catch
            {
                // ignore any viewbag assignment errors
            }

            return student;
        }

        // Dashboard
        public async Task<IActionResult> StudDashboard()
        {
            Console.WriteLine("[StudDashboard] Method called");
            Console.WriteLine($"[StudDashboard] User authenticated: {User.Identity?.IsAuthenticated}");
            
            var student = await GetCurrentStudent();
            if (student == null)
            {
                Console.WriteLine("[StudDashboard] Student is null - redirecting to login");
                Console.WriteLine($"[StudDashboard] Current claims: {string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}"))}");
                return RedirectToAction("Login", "Account");
            }

            Console.WriteLine($"[StudDashboard] Loading dashboard for student: {student.StudentId}");

            // Get attendance data for this student
            var allAttendances = await _context.Attendances
                .Where(a => a.StudentId == student.StudentId)
                .ToListAsync();

            // Calculate attendance statistics
            var totalClasses = allAttendances.Count;
            var presentCount = allAttendances.Count(a => a.Status == "Present");
            var absentCount = allAttendances.Count(a => a.Status == "Absent");
            var leaveCount = allAttendances.Count(a => a.Status == "Leave");
            var attendanceRate = totalClasses > 0 ? Math.Round((double)presentCount / totalClasses * 100, 2) : 0;

            // Get enrollment statistics
            var enrolledClasses = student.Enrollments?.Count ?? 0;

            // Get recent attendances (last 5 records, ordered by date descending)
            var recentAttendances = await _context.Attendances
                .Include(a => a.Class)
                    .ThenInclude(c => c.Subject)
                .Include(a => a.Class)
                    .ThenInclude(c => c.Teacher)
                        .ThenInclude(t => t.User)
                .Where(a => a.StudentId == student.StudentId)
                .OrderByDescending(a => a.Date)
                .Take(5)
                .ToListAsync();

            // Store in ViewBag for use in view
            ViewBag.TotalClasses = totalClasses;
            ViewBag.PresentCount = presentCount;
            ViewBag.AbsentCount = absentCount;
            ViewBag.LeaveCount = leaveCount;
            ViewBag.AttendanceRate = attendanceRate;
            ViewBag.EnrolledClasses = enrolledClasses;
            ViewBag.RecentAttendances = recentAttendances;
            
            // Get attendance data for different time periods
            var today = DateTime.Today;
            var last7Days = today.AddDays(-6); // includes today = 7 days
            var last30Days = today.AddDays(-29); // includes today = 30 days
            var last3Months = today.AddMonths(-3);

            var attendances7Days = allAttendances.Where(a => a.Date.Date >= last7Days).ToList();
            var attendances30Days = allAttendances.Where(a => a.Date.Date >= last30Days).ToList();
            var attendances3Months = allAttendances.Where(a => a.Date.Date >= last3Months).ToList();

            // Time period data as JSON for JavaScript
            ViewBag.Attendances7Days = System.Text.Json.JsonSerializer.Serialize(attendances7Days.GroupBy(a => a.Date.Date).Select(g => new { Date = g.Key, Present = g.Count(a => a.Status == "Present"), Absent = g.Count(a => a.Status == "Absent"), Leave = g.Count(a => a.Status == "Leave") }));
            ViewBag.Attendances30Days = System.Text.Json.JsonSerializer.Serialize(attendances30Days.GroupBy(a => a.Date.Date).Select(g => new { Date = g.Key, Present = g.Count(a => a.Status == "Present"), Absent = g.Count(a => a.Status == "Absent"), Leave = g.Count(a => a.Status == "Leave") }));
            ViewBag.Attendances3Months = System.Text.Json.JsonSerializer.Serialize(attendances3Months.GroupBy(a => a.Date.Date).Select(g => new { Date = g.Key, Present = g.Count(a => a.Status == "Present"), Absent = g.Count(a => a.Status == "Absent"), Leave = g.Count(a => a.Status == "Leave") }));
            
            ViewBag.ActiveMenu = "Dashboard";

            return View("StudDashboard", student);
        }

        // Attendance History
        public async Task<IActionResult> StudAttendanceHistory(string filterClass, string filterMonth, string filterStatus, string tableSearch)
        {
            var student = await GetCurrentStudent();
            if (student == null)
                return RedirectToAction("Login", "Account");

            // Base query for current student's attendance with related Class and Subject
            var query = _context.Attendances
                .Include(a => a.Class)
                    .ThenInclude(c => c.Subject)
                .Where(a => a.StudentId == student.StudentId)
                .AsQueryable();

            // Filter by class name
            if (!string.IsNullOrWhiteSpace(filterClass))
            {
                var fc = filterClass.Trim();
                query = query.Where(a => a.Class != null && a.Class.ClassName == fc);
            }

            // Filter by status
            if (!string.IsNullOrWhiteSpace(filterStatus))
            {
                var fs = filterStatus.Trim();
                query = query.Where(a => a.Status == fs);
            }

            // Filter by month (expecting "yyyy-MM")
            if (!string.IsNullOrWhiteSpace(filterMonth))
            {
                // parse using yyyy-MM by appending -01
                if (DateTime.TryParseExact(filterMonth + "-01", "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var firstOfMonth))
                {
                    var start = new DateTime(firstOfMonth.Year, firstOfMonth.Month, 1);
                    var end = start.AddMonths(1);
                    query = query.Where(a => a.Date >= start && a.Date < end);
                }
            }

        

            // Free-text search against class name or subject name
            if (!string.IsNullOrWhiteSpace(tableSearch))
            {
                var s = tableSearch.Trim();
                query = query.Where(a =>
                    (a.Class != null && a.Class.ClassName.Contains(s)) ||
                    (a.Class != null && a.Class.Subject != null && a.Class.Subject.SubjectName.Contains(s)) ||
                    a.Status.Contains(s));
            }

            var attendances = await query
                .OrderByDescending(a => a.Date)
                .ToListAsync();

            ViewBag.ActiveMenu = "Attendance";
            ViewBag.ActiveSubmenu = "StudAttendanceHistory";
            return View("StudAttendanceHistory", attendances);
        }

        // Export Attendance History to PDF using QuestPDF
        [HttpGet]
        public async Task<IActionResult> ExportAttendanceHistoryPdf(string? subject = null, string? date = null, string? status = null)
        {
            var student = await GetCurrentStudent();
            if (student == null)
                return RedirectToAction("Login", "Account");

            // Get all attendance records for the student
            var query = _context.Attendances
                .Include(a => a.Class)
                    .ThenInclude(c => c.Subject)
                .Where(a => a.StudentId == student.StudentId);

            // Apply filters if provided
            if (!string.IsNullOrEmpty(subject))
            {
                query = query.Where(a => a.Class != null && a.Class.Subject != null && 
                    a.Class.Subject.SubjectName.ToLower() == subject.ToLower());
            }

            if (!string.IsNullOrEmpty(date))
            {
                if (DateTime.TryParse(date, out DateTime filterDate))
                {
                    query = query.Where(a => a.Date.Date == filterDate.Date);
                }
            }

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(a => a.Status == status);
            }

            var attendances = await query
                .OrderByDescending(a => a.Date)
                .ToListAsync();

            // Convert to DTO
            var reportItems = attendances.Select(a => new AttendanceReportItem
            {
                Date = a.Date.ToString("yyyy-MM-dd"),
                Day = a.Date.DayOfWeek.ToString(),
                ClassName = a.Class?.ClassName ?? "-",
                SubjectName = a.Class?.Subject?.SubjectName ?? "-",
                TimeTaken = a.TakenOn != default ? a.TakenOn.ToString("HH:mm") : "-",
                Status = a.Status
            }).ToList();

            // Generate PDF
            var pdfBytes = _pdfService.GenerateAttendanceHistoryPdf(
                reportItems, 
                student.User.FullName, 
                student.StudentId
            );

            // Return PDF file
            var fileName = $"attendance-history-{student.StudentId}-{DateTime.Now:yyyy-MM-dd}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }

        // Take Attendance - Main Page
        public async Task<IActionResult> StudTakeAttendance()
        {
            var student = await GetCurrentStudent();
            if (student == null)
                return RedirectToAction("Login", "Account");

            ViewBag.ActiveMenu = "Attendance";
            ViewBag.ActiveSubmenu = "TakeAttendance";
            ViewBag.StudentId = student.StudentId;
            ViewBag.StudentName = student.User.FullName;

            return View("StudTakeAttendance", student);
        }

        // Submit Attendance via PIN (AJAX endpoint for students)
        [HttpPost]
        public async Task<IActionResult> SubmitStudentAttendance(string pinCode)
        {
            try
            {
                Console.WriteLine($"SubmitStudentAttendance called with PIN: {pinCode}");
                
                // Get current logged-in student
                var student = await GetCurrentStudent();
                if (student == null)
                {
                    Console.WriteLine("Student not found - not authenticated");
                    return Json(new { success = false, message = "Not authenticated. Please login." });
                }

                Console.WriteLine($"Student authenticated: {student.StudentId} - {student.User.FullName}");

                // Find active session with this PIN
                var session = await _context.AttendanceSessions
                    .Include(s => s.Class)
                    .FirstOrDefaultAsync(s => s.PinCode == pinCode && s.IsActive);

                if (session == null)
                {
                    Console.WriteLine($"No active session found for PIN: {pinCode}");
                    return Json(new { success = false, message = "Invalid PIN code" });
                }

                Console.WriteLine($"Session found: {session.SessionId} for class {session.Class.ClassName}");

                // Check if PIN has expired
                if (session.ExpiryDate < DateTime.Now)
                {
                    Console.WriteLine($"PIN expired. Expiry: {session.ExpiryDate}, Now: {DateTime.Now}");
                    return Json(new { success = false, message = "PIN code has expired" });
                }

                // Validate class has schedule
                if (!session.Class.StartTime.HasValue || !session.Class.EndTime.HasValue)
                {
                    Console.WriteLine("Class schedule not configured");
                    return Json(new { success = false, message = "Class schedule not configured" });
                }

                // Check if current time is within class hours
                var currentTime = DateTime.Now.TimeOfDay;
                var classStartTime = session.Class.StartTime.Value;
                var classEndTime = session.Class.EndTime.Value;

                Console.WriteLine($"Time check - Current: {currentTime}, Start: {classStartTime}, End: {classEndTime}");

                if (currentTime < classStartTime || currentTime > classEndTime)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Attendance can only be taken during class hours ({classStartTime:hh\\:mm} - {classEndTime:hh\\:mm}). Current time: {DateTime.Now:hh\\:mm tt}"
                    });
                }

                // Check if today matches the class day
                var currentDayOfWeek = DateTime.Now.DayOfWeek.ToString();
                if (!string.IsNullOrEmpty(session.Class.Day) && !session.Class.Day.Equals(currentDayOfWeek, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"Wrong day - Class: {session.Class.Day}, Today: {currentDayOfWeek}");
                    return Json(new
                    {
                        success = false,
                        message = $"Attendance can only be taken on the scheduled day: {session.Class.Day}"
                    });
                }

                // Check if student is enrolled in this class
                var isEnrolled = student.Enrollments.Any(e => e.ClassId == session.ClassId);
                if (!isEnrolled)
                {
                    Console.WriteLine($"Student {student.StudentId} not enrolled in class {session.ClassId}");
                    return Json(new { success = false, message = "You are not enrolled in this class" });
                }

                // Check if already marked attendance for today
                var existingAttendance = await _context.Attendances
                    .FirstOrDefaultAsync(a => a.StudentId == student.StudentId &&
                                              a.ClassId == session.ClassId &&
                                              a.Date.Date == DateTime.Today);

                if (existingAttendance != null)
                {
                    Console.WriteLine($"Attendance already marked for student {student.StudentId} today");
                    return Json(new { success = false, message = "You have already marked attendance for this class today" });
                }

                // Generate unique attendance ID using IdGenerator
                var attId = IdGenerator.GenerateAttendanceId(_context);
                Console.WriteLine($"Generated attendance ID: {attId}");

                var attendance = new Attendance
                {
                    AttendanceId = attId,
                    StudentId = student.StudentId,
                    ClassId = session.ClassId,
                    Date = DateTime.Now,
                    TakenOn = DateTime.Now,
                    Status = "Present",
                    MarkedByTeacherId = session.CreatedByTeacherId
                };

                _context.Attendances.Add(attendance);
                await _context.SaveChangesAsync();

                Console.WriteLine($"Attendance marked successfully: {attId}");

                return Json(new
                {
                    success = true,
                    message = $"Attendance marked successfully!",
                    studentName = student.User.FullName,
                    className = session.Class.ClassName,
                    time = DateTime.Now.ToString("hh:mm tt"),
                    date = DateTime.Now.ToString("MMM dd, yyyy")
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SubmitStudentAttendance: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // Classes
        public async Task<IActionResult> StudClasses()
        {
            var student = await GetCurrentStudent();
            if (student == null)
                return RedirectToAction("Login", "Account");

            ViewBag.ActiveMenu = "Classes";
            return View("StudClasses", student);
        }

        // Class Detail
        public async Task<IActionResult> StudClassDetail(string id)
        {
            var student = await GetCurrentStudent();
            if (student == null)
                return RedirectToAction("Login", "Account");

            var classInfo = await _context.Classes
                .Include(c => c.Teacher)
                    .ThenInclude(t => t.User)
                .Include(c => c.Subject)
                .Include(c => c.Enrollments)
                    .ThenInclude(e => e.Student)
                        .ThenInclude(s => s.User)
                .FirstOrDefaultAsync(c => c.ClassId == id);

            if (classInfo == null)
                return NotFound();

            ViewBag.ActiveMenu = "Classes";
            return View("StudClassDetail", classInfo);
        }

        // Student Profile
        public async Task<IActionResult> StudProfile()
        {
            var student = await GetCurrentStudent();
            if (student == null)
                return RedirectToAction("Login", "Account");

            ViewBag.ActiveMenu = "Profile";
            return View("StudProfile", student);
        }

        // Settings
        public async Task<IActionResult> StudSettings()
        {
            var student = await GetCurrentStudent();
            if (student == null)
                return RedirectToAction("Login", "Account");

            ViewBag.ActiveMenu = "Settings";
            ViewBag.ActiveSubmenu = "Settings";
            return View("StudSettings", student);
        }

        // Settings - POST Handler
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StudSettings(string fullName, string email, string? phoneNumber, 
            DateTime? dateOfBirth, string? gender, string? address, IFormFile? profilePicture)
        {
            var student = await GetCurrentStudent();
            if (student == null)
                return RedirectToAction("Login", "Account");

            // Validate required fields
            if (string.IsNullOrWhiteSpace(fullName))
            {
                TempData["ErrorMessage"] = "Full name is required.";
                return View("StudSettings", student);
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["ErrorMessage"] = "Email is required.";
                return View("StudSettings", student);
            }

            try
            {
                // Handle profile picture upload if provided
                if (profilePicture != null && profilePicture.Length > 0)
                {
                    try
                    {
                        // Validate file type
                        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                        var fileExtension = Path.GetExtension(profilePicture.FileName).ToLower();
                        if (!allowedExtensions.Contains(fileExtension))
                        {
                            TempData["ErrorMessage"] = "Invalid file type. Please upload JPG, PNG, GIF, or WEBP image.";
                            return View("StudSettings", student);
                        }

                        // Validate file size (5 MB)
                        if (profilePicture.Length > 5 * 1024 * 1024)
                        {
                            TempData["ErrorMessage"] = "File size must not exceed 5 MB.";
                            return View("StudSettings", student);
                        }

                        // Delete old picture if exists and is not default
                        if (!string.IsNullOrEmpty(student.User.ProfilePicture) && 
                            !student.User.ProfilePicture.StartsWith("/images/"))
                        {
                            await _s3Service.DeleteFileAsync(student.User.ProfilePicture);
                        }

                        // Upload new picture to S3
                        var profilePictureUrl = await _s3Service.UploadFileAsync(profilePicture, student.User.UserId);
                        student.User.ProfilePicture = profilePictureUrl;
                        
                        Console.WriteLine($"Profile picture uploaded successfully: {profilePictureUrl}");
                    }
                    catch (Exception uploadEx)
                    {
                        Console.WriteLine($"Error uploading profile picture: {uploadEx.Message}");
                        TempData["ErrorMessage"] = $"Failed to upload profile picture: {uploadEx.Message}";
                        return View("StudSettings", student);
                    }
                }

                // Update user information
                student.User.FullName = fullName;
                student.User.Email = email;
                student.User.PhoneNumber = phoneNumber;
                student.User.DateOfBirth = dateOfBirth;
                student.User.Gender = gender;

                // Update student information
                if (!string.IsNullOrWhiteSpace(gender))
                {
                    student.Gender = gender;
                }
                if (dateOfBirth.HasValue)
                {
                    student.DateOfBirth = dateOfBirth.Value;
                }

                _context.Update(student);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Profile updated successfully!";
                return RedirectToAction(nameof(StudSettings));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating student profile: {ex.Message}");
                TempData["ErrorMessage"] = $"Error updating profile: {ex.Message}";
                return View("StudSettings", student);
            }
        }

        // Change Password
        public async Task<IActionResult> StudChangePassword()
        {
            var student = await GetCurrentStudent();
            if (student == null)
                return RedirectToAction("Login", "Account");

            ViewBag.ActiveMenu = "Settings";
            ViewBag.ActiveSubmenu = "ChangePassword";
            return View("StudChangePassword", student);
        }

        // Change Password - POST Handler
        [HttpPost]
        public async Task<IActionResult> StudChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            try
            {
                var student = await GetCurrentStudent();
                if (student == null)
                {
                    return Json(new { success = false, message = "Not authenticated. Please login." });
                }

                // Validation
                if (string.IsNullOrWhiteSpace(currentPassword))
                {
                    return Json(new { success = false, message = "Current password is required." });
                }

                if (string.IsNullOrWhiteSpace(newPassword))
                {
                    return Json(new { success = false, message = "New password is required." });
                }

                if (newPassword != confirmPassword)
                {
                    return Json(new { success = false, message = "New password and confirm password do not match." });
                }

                if (currentPassword == newPassword)
                {
                    return Json(new { success = false, message = "New password must be different from current password." });
                }

                // Validate password strength using Helper method
                var (isValid, errors) = _helper.ValidatePasswordStrength(newPassword);
                if (!isValid)
                {
                    var errorMessage = "Password does not meet security requirements:\n" + string.Join("\n", errors);
                    return Json(new { success = false, message = errorMessage });
                }

                // Get user
                var user = student.User;
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                // Verify current password
                if (!_helper.VerifyPassword(user.PasswordHash, currentPassword))
                {
                    Console.WriteLine($"Password verification failed for user {user.UserId}");
                    return Json(new { success = false, message = "Current password is incorrect." });
                }

                // Hash new password
                var newPasswordHash = _helper.HashPassword(newPassword);

                // Update password
                user.PasswordHash = newPasswordHash;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                Console.WriteLine($"Password changed successfully for user {user.UserId}");

                return Json(new
                {
                    success = true,
                    message = "Password changed successfully!"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in StudChangePassword POST: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // Apply Medical Leave
        public async Task<IActionResult> StudApplyMedicalLeave()
        {
            var student = await GetCurrentStudent();
            if (student == null)
                return RedirectToAction("Login", "Account");

            ViewBag.ActiveMenu = "Attendance";
            ViewBag.ActiveSubmenu = "ApplyMedicalLeave";
            return View("StudApplyMedicalLeave", student);
        }

        // Apply Medical Leave - POST Handler
        [HttpPost]
        public async Task<IActionResult> StudApplyMedicalLeave(DateTime startDate, DateTime endDate, string reason, IFormFile document)
        {
            try
            {
                var student = await GetCurrentStudent();
                if (student == null)
                {
                    return Json(new { success = false, message = "Not authenticated. Please login." });
                }

                // Validation
                if (startDate > endDate)
                {
                    return Json(new { success = false, message = "End date must be equal to or after start date." });
                }

                if (string.IsNullOrWhiteSpace(reason))
                {
                    return Json(new { success = false, message = "Please provide a reason for your leave." });
                }

                // Calculate number of days
                var totalDays = (int)Math.Ceiling((endDate - startDate).TotalDays) + 1;

                // Create leave application
                var leaveId = IdGenerator.GenerateLeaveApplicationId(_context);
                var leaveApplication = new LeaveApplication
                {
                    LeaveId = leaveId,
                    UserId = student.UserId,
                    StartDate = startDate,
                    EndDate = endDate,
                    TotalDays = totalDays,
                    Reason = reason,
                    Status = "Pending",
                    CreatedDate = DateTime.Now
                };

                // Handle document upload if provided
                if (document != null && document.Length > 0)
                {
                    try
                    {
                        // Validate file type
                        var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx" };
                        var fileExtension = Path.GetExtension(document.FileName).ToLower();
                        if (!allowedExtensions.Contains(fileExtension))
                        {
                            return Json(new { success = false, message = "File format not allowed. Accepted: PDF, JPG, PNG, DOC, DOCX" });
                        }

                        // Upload to S3 in the "Leave" folder
                        var documentUrl = await _s3Service.UploadDocumentAsync(document, "Leave");
                        leaveApplication.DocumentPaths = documentUrl;

                        Console.WriteLine($"Document uploaded to S3: {documentUrl}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error uploading document to S3: {ex.Message}");
                        return Json(new { success = false, message = $"Error uploading file: {ex.Message}" });
                    }
                }

                // Save to database
                _context.LeaveApplications.Add(leaveApplication);

                // Create notifications for admins
                var admins = await _context.Users.Where(u => u.UserType == "Admin").ToListAsync();
                foreach (var admin in admins)
                {
                    var adminNotificationId = IdGenerator.GenerateNotificationId(_context);
                    var adminNotification = new Notification
                    {
                        NotificationId = adminNotificationId,
                        UserId = admin.UserId,
                        Type = "Leave Application",
                        Description = $"{student.User.FullName} has applied for leave from {startDate:dd MMM yyyy} to {endDate:dd MMM yyyy}. Reason: {reason}",
                        RelatedEntityId = leaveId,
                        Status = "unread",
                        CreatedDate = DateTime.Now
                    };
                    _context.Notifications.Add(adminNotification);
                }

                // Create notifications for teachers of enrolled classes
                var enrolledClasses = await _context.Enrollments
                    .Include(e => e.Class)
                        .ThenInclude(c => c.Teacher)
                    .Where(e => e.StudentId == student.StudentId && e.UnenrolledDate == null)
                    .ToListAsync();

                foreach (var enrollment in enrolledClasses)
                {
                    if (enrollment.Class?.Teacher?.UserId != null)
                    {
                        var teacherNotificationId = IdGenerator.GenerateNotificationId(_context);
                        var teacherNotification = new Notification
                        {
                            NotificationId = teacherNotificationId,
                            UserId = enrollment.Class.Teacher.UserId,
                            Type = "Student Leave Application",
                            Description = $"{student.User.FullName} from class {enrollment.Class.ClassName} has applied for leave from {startDate:dd MMM yyyy} to {endDate:dd MMM yyyy}. Reason: {reason}",
                            RelatedEntityId = leaveId,
                            Status = "unread",
                            CreatedDate = DateTime.Now
                        };
                        _context.Notifications.Add(teacherNotification);
                    }
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"Medical leave application submitted successfully! ({totalDays} day(s))"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in StudApplyMedicalLeave POST: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // Announcements (using Notifications table)
        public async Task<IActionResult> StudAnnouncements()
        {
            var student = await GetCurrentStudent();
            if (student == null)
                return RedirectToAction("Login", "Account");

            // Get notifications for this student
            var notifications = await _context.Notifications
                .Include(n => n.User)
                .Where(n => n.UserId == student.UserId)
                .OrderByDescending(n => n.Status == "unread")
                .ThenByDescending(n => n.CreatedDate)
                .ToListAsync();

            // Calculate stats
            ViewBag.TotalAnnouncements = notifications.Count;
            ViewBag.UnreadCount = notifications.Count(n => n.Status == "unread");
            ViewBag.ReadCount = notifications.Count(n => n.Status == "read");

            ViewBag.ActiveMenu = "Announcements";
            return View("StudAnnouncements", notifications);
        }


        // Mark notification as read
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> MarkNotificationAsRead(string notificationId)
        {
            var student = await GetCurrentStudent();
            if (student == null)
                return Json(new { success = false, message = "Not authenticated" });

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == student.UserId);

            if (notification == null)
                return Json(new { success = false, message = "Notification not found" });

            notification.Status = "read";
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        // Mark all notifications as read
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> MarkAllNotificationsAsRead()
        {
            var student = await GetCurrentStudent();
            if (student == null)
                return Json(new { success = false, message = "Not authenticated" });

            var notifications = await _context.Notifications
                .Where(n => n.UserId == student.UserId && n.Status == "unread")
                .ToListAsync();

            foreach (var notification in notifications)
            {
                notification.Status = "read";
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, count = notifications.Count });
        }
    }
}
