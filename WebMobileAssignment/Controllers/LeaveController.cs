using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebMobileAssignment.Models;
using WebMobileAssignment.Services;
using System.Text.Json;

namespace WebMobileAssignment.Controllers
{
    public class LeaveController : Controller
    {
        private readonly DB _context;
        private readonly Helper _helper;
        private readonly S3Service _s3Service;

        public LeaveController(DB context, Helper helper, S3Service s3Service)
        {
            _context = context;
            _helper = helper;
            _s3Service = s3Service;
        }

        // ==================== STUDENT LEAVE APPLICATION ====================

        [Authorize(Roles = "Student")]
        public async Task<IActionResult> StudentLeaveIndex()
        {
            var userEmail = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);

            if (user == null) return RedirectToAction("Login", "Account");

            var leaves = await _context.LeaveApplications
                .Where(l => l.UserId == user.UserId)
                .OrderByDescending(l => l.CreatedDate)
                .ToListAsync();

            ViewBag.UserName = user.FullName;
            return View(leaves);
        }

        [Authorize(Roles = "Student")]
        public IActionResult StudentApplyLeave()
        {
            return View();
        }

        [Authorize(Roles = "Student")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StudentApplyLeave(DateTime startDate, DateTime endDate, 
            string reason, List<IFormFile>? documents)
        {
            Console.WriteLine($"[LeaveController] StudentApplyLeave POST started");
            
            var userEmail = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);

            if (user == null) return RedirectToAction("Login", "Account");

            Console.WriteLine($"[LeaveController] User: {user.FullName} ({user.UserId})");

            // Validation
            if (startDate < DateTime.Today)
            {
                ModelState.AddModelError("", "Start date cannot be in the past");
            }

            if (endDate < startDate)
            {
                ModelState.AddModelError("", "End date must be after start date");
            }

            var totalDays = (int)(endDate - startDate).TotalDays + 1;

            // Check for overlapping leave (exclude rejected applications)
            var overlapping = await _context.LeaveApplications
                .Where(l => l.UserId == user.UserId && 
                           l.Status != "Rejected" &&  // Allow reapplication if previous was rejected
                           ((startDate >= l.StartDate && startDate <= l.EndDate) ||
                            (endDate >= l.StartDate && endDate <= l.EndDate)))
                .AnyAsync();

            if (overlapping)
            {
                ModelState.AddModelError("", "You have an overlapping leave application for this period");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var leaveCount = await _context.LeaveApplications.CountAsync();
                    var leaveId = $"LEAVE{(leaveCount + 1):D5}";
                    
                    Console.WriteLine($"[LeaveController] Generated Leave ID: {leaveId}");

                    var documentUrls = new List<string>();

                    // Upload documents to S3 if provided
                    if (documents != null && documents.Any())
                    {
                        Console.WriteLine($"[LeaveController] Processing {documents.Count} document(s)");
                        
                        foreach (var doc in documents.Take(3)) // Max 3 files
                        {
                            Console.WriteLine($"[LeaveController] Processing file: {doc.FileName}, Size: {doc.Length} bytes");
                            
                            if (doc.Length > 0 && doc.Length <= 5 * 1024 * 1024) // 5MB max
                            {
                                var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx" };
                                var extension = Path.GetExtension(doc.FileName).ToLower();

                                if (allowedExtensions.Contains(extension))
                                {
                                    try
                                    {
                                        Console.WriteLine($"[LeaveController] Uploading {doc.FileName} to S3...");
                                        var s3Path = await _s3Service.UploadDocumentAsync(doc, $"leave_documents/{leaveId}");
                                        documentUrls.Add(s3Path);
                                        Console.WriteLine($"[LeaveController] Successfully uploaded: {s3Path}");
                                    }
                                    catch (Exception uploadEx)
                                    {
                                        Console.WriteLine($"[LeaveController] UPLOAD ERROR for {doc.FileName}: {uploadEx.Message}");
                                        Console.WriteLine($"[LeaveController] Stack trace: {uploadEx.StackTrace}");
                                        ModelState.AddModelError("", $"Failed to upload {doc.FileName}: {uploadEx.Message}");
                                        // Don't continue with other documents on error - show error to user
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"[LeaveController] Skipped invalid extension: {extension}");
                                    ModelState.AddModelError("", $"File {doc.FileName} has invalid extension. Allowed: PDF, JPG, PNG, DOC, DOCX");
                                }
                            }
                            else if (doc.Length > 5 * 1024 * 1024)
                            {
                                Console.WriteLine($"[LeaveController] File too large: {doc.FileName}");
                                ModelState.AddModelError("", $"File {doc.FileName} exceeds 5MB limit");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("[LeaveController] No documents provided");
                    }

                    // Only save if no errors occurred during upload
                    if (!ModelState.IsValid)
                    {
                        Console.WriteLine("[LeaveController] Validation failed, returning to form");
                        return View();
                    }

                    var leave = new LeaveApplication
                    {
                        LeaveId = leaveId,
                        UserId = user.UserId,
                        StartDate = startDate,
                        EndDate = endDate,
                        TotalDays = totalDays,
                        Reason = reason,
                        Status = "Pending",
                        CreatedDate = DateTime.Now,
                        DocumentPaths = documentUrls.Any() ? JsonSerializer.Serialize(documentUrls) : null
                    };

                    Console.WriteLine($"[LeaveController] Saving leave application to database...");
                    Console.WriteLine($"[LeaveController] DocumentPaths: {leave.DocumentPaths ?? "NULL"}");

                    _context.LeaveApplications.Add(leave);
                    await _context.SaveChangesAsync();

                    Console.WriteLine($"[LeaveController] Leave application saved successfully");

                    // Send notification to admin
                    var adminUsers = await _context.Users
                        .Where(u => u.UserType == "Admin")
                        .ToListAsync();

                    foreach (var admin in adminUsers)
                    {
                        var notificationId = IdGenerator.GenerateNotificationId(_context);
                        var notification = new Notification
                        {
                            NotificationId = notificationId,
                            UserId = admin.UserId,
                            Type = "Leave Application",
                            Description = $"New leave application from {user.FullName} for {totalDays} day(s) ({startDate:dd MMM} - {endDate:dd MMM})",
                            RelatedEntityId = leave.LeaveId,
                            Status = "unread",
                            CreatedDate = DateTime.Now
                        };
                        _context.Notifications.Add(notification);
                    }

                    // Send notification to teachers of classes the student is enrolled in
                    var student = await _context.Students
                        .Include(s => s.Enrollments)
                        .ThenInclude(e => e.Class)
                        .ThenInclude(c => c.Teacher)
                        .ThenInclude(t => t.User)
                        .FirstOrDefaultAsync(s => s.UserId == user.UserId);
                    
                    if (student != null && student.Enrollments != null)
                    {
                        var teachersToNotify = student.Enrollments
                            .Where(e => e.UnenrolledDate == null && e.Class.TeacherId != null)
                            .Select(e => e.Class.Teacher)
                            .Distinct()
                            .ToList();
                        
                        foreach (var teacher in teachersToNotify)
                        {
                            if (teacher?.User != null)
                            {
                                var notificationId = IdGenerator.GenerateNotificationId(_context);
                                var teacherNotification = new Notification
                                {
                                    NotificationId = notificationId,
                                    UserId = teacher.UserId,
                                    Type = "Student Leave Application",
                                    Description = $"Student {user.FullName} from your class has applied for leave ({startDate:dd MMM} - {endDate:dd MMM}, {totalDays} day(s))",
                                    RelatedEntityId = leave.LeaveId,
                                    Status = "unread",
                                    CreatedDate = DateTime.Now
                                };
                                _context.Notifications.Add(teacherNotification);
                            }
                        }
                        Console.WriteLine($"[LeaveController] Notifications sent to {teachersToNotify.Count} teacher(s)");
                    }

                    await _context.SaveChangesAsync();

                    Console.WriteLine($"[LeaveController] Notifications sent to {adminUsers.Count} admin(s)");

                    TempData["SuccessMessage"] = "Leave application submitted successfully!";
                    return RedirectToAction(nameof(StudentLeaveIndex));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LeaveController] FATAL ERROR: {ex.Message}");
                    Console.WriteLine($"[LeaveController] Stack trace: {ex.StackTrace}");
                    ModelState.AddModelError("", $"Error: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("[LeaveController] ModelState invalid:");
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    Console.WriteLine($"  - {error.ErrorMessage}");
                }
            }

            return View();
        }

        [Authorize(Roles = "Student")]
        public async Task<IActionResult> StudentLeaveDetails(string id)
        {
            var userEmail = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);

            if (user == null) return RedirectToAction("Login", "Account");

            var leave = await _context.LeaveApplications
                .FirstOrDefaultAsync(l => l.LeaveId == id && l.UserId == user.UserId);

            if (leave == null) return NotFound();

            // Parse document paths - use direct URLs (no pre-signing needed)
            var documentUrls = new List<string>();
            if (!string.IsNullOrEmpty(leave.DocumentPaths))
            {
                try
                {
                    documentUrls = JsonSerializer.Deserialize<List<string>>(leave.DocumentPaths) ?? new List<string>();
                }
                catch
                {
                    documentUrls = leave.DocumentPaths.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                }
            }
            ViewBag.DocumentUrls = documentUrls;

            return View(leave);
        }
    }
}
