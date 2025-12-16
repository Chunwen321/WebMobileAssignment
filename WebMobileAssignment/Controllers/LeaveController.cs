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

        // ==================== ADMIN LEAVE MANAGEMENT ====================

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> LeaveIndex(string? status, string? search, DateTime? startDate, DateTime? endDate)
        {
            ViewBag.ActiveMenu = "LeaveManagement";
            ViewBag.Title = "Leave Management";

            var query = _context.LeaveApplications
                .Include(l => l.User)
                .AsQueryable();

            // Filter by status
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(l => l.Status == status);
                ViewBag.SelectedStatus = status;
            }

            // Search by student name or ID
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(l => l.User.FullName.Contains(search) || 
                                        l.UserId.Contains(search) ||
                                        l.LeaveId.Contains(search));
                ViewBag.SearchTerm = search;
            }

            // Filter by date range
            if (startDate.HasValue)
            {
                query = query.Where(l => l.StartDate >= startDate.Value);
                ViewBag.StartDate = startDate.Value.ToString("yyyy-MM-dd");
            }

            if (endDate.HasValue)
            {
                query = query.Where(l => l.EndDate <= endDate.Value);
                ViewBag.EndDate = endDate.Value.ToString("yyyy-MM-dd");
            }

            var leaves = await query
                .OrderByDescending(l => l.CreatedDate)
                .ToListAsync();

            // Calculate statistics
            var allLeaves = await _context.LeaveApplications.ToListAsync();
            ViewBag.TotalApplications = allLeaves.Count;
            ViewBag.PendingCount = allLeaves.Count(l => l.Status == "Pending");
            ViewBag.ApprovedCount = allLeaves.Count(l => l.Status == "Approved");
            ViewBag.RejectedCount = allLeaves.Count(l => l.Status == "Rejected");

            return View(leaves);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> LeaveDetails(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var leave = await _context.LeaveApplications
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.LeaveId == id);

            if (leave == null) return NotFound();

            ViewBag.ActiveMenu = "LeaveManagement";
            ViewBag.Title = "Leave Application Details";

            // Get student info if user is a student
            var student = await _context.Students
                .Include(s => s.Parent)
                    .ThenInclude(p => p.User)
                .FirstOrDefaultAsync(s => s.UserId == leave.UserId);

            ViewBag.Student = student;

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
                    // If not JSON, try comma-separated
                    documentUrls = leave.DocumentPaths.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                }
            }
            ViewBag.DocumentUrls = documentUrls;

            return View(leave);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveLeave(string leaveId, string? remarks)
        {
            var leave = await _context.LeaveApplications
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.LeaveId == leaveId);

            if (leave == null)
            {
                TempData["ErrorMessage"] = "Leave application not found.";
                return RedirectToAction(nameof(LeaveIndex));
            }

            if (leave.Status != "Pending")
            {
                TempData["ErrorMessage"] = "Only pending applications can be approved.";
                return RedirectToAction(nameof(LeaveDetails), new { id = leaveId });
            }

            try
            {
                leave.Status = "Approved";
                leave.Remarks = remarks; // Save admin remarks

                // Create notification for student
                var notificationCount = await _context.Notifications.CountAsync();
                var notification = new Notification
                {
                    NotificationId = $"NOTIF{(notificationCount + 1):D5}",
                    UserId = leave.UserId,
                    Description = $"Your leave application from {leave.StartDate:dd MMM yyyy} to {leave.EndDate:dd MMM yyyy} has been approved." + 
                                  (string.IsNullOrEmpty(remarks) ? "" : $" Remarks: {remarks}"),
                    Status = "unread",
                    CreatedDate = DateTime.Now
                };
                _context.Notifications.Add(notification);

                await _context.SaveChangesAsync();

                // Send email notification
                try
                {
                    var mailMessage = new System.Net.Mail.MailMessage
                    {
                        To = { leave.User.Email },
                        Subject = "Leave Application Approved - Tuition Attendance System",
                        Body = $@"
                            <html>
                              <body style='font-family: Arial, sans-serif;'>
                                <div style='max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f8f9fa;'>
                                  <div style='background-color: white; padding: 30px; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1)'>
                                    <h2 style='color: #28a745;'>Leave Application Approved</h2>
                                    <p>Dear <strong>{leave.User.FullName}</strong>,</p>
                                    <p>Your leave application has been <strong style='color: #28a745;'>approved</strong>.</p>
                                    
                                    <div style='background-color: #f8f9fa; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                                      <p style='margin: 5px 0;'><strong>Leave Period:</strong> {leave.StartDate:dd MMM yyyy} to {leave.EndDate:dd MMM yyyy}</p>
                                      <p style='margin: 5px 0;'><strong>Total Days:</strong> {leave.TotalDays} day(s)</p>
                                      <p style='margin: 5px 0;'><strong>Reason:</strong> {leave.Reason}</p>
                                      {(string.IsNullOrEmpty(remarks) ? "" : $"<p style='margin: 5px 0;'><strong>Admin Remarks:</strong> {remarks}</p>")}
                                    </div>

                                    <p style='color: #6c757d; font-size: 12px; margin-top: 30px;'>
                                      This is an automated email from the Tuition Attendance System.
                                    </p>
                                  </div>
                                </div>
                              </body>
                            </html>",
                        IsBodyHtml = true
                    };
                    _helper.SendEmail(mailMessage);
                }
                catch (Exception emailEx)
                {
                    Console.WriteLine($"Warning: Failed to send email: {emailEx.Message}");
                }

                TempData["SuccessMessage"] = $"Leave application for {leave.User.FullName} has been approved.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error approving leave: {ex.Message}";
            }

            return RedirectToAction(nameof(LeaveDetails), new { id = leaveId });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectLeave(string leaveId, string? remarks)
        {
            var leave = await _context.LeaveApplications
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.LeaveId == leaveId);

            if (leave == null)
            {
                TempData["ErrorMessage"] = "Leave application not found.";
                return RedirectToAction(nameof(LeaveIndex));
            }

            if (leave.Status != "Pending")
            {
                TempData["ErrorMessage"] = "Only pending applications can be rejected.";
                return RedirectToAction(nameof(LeaveDetails), new { id = leaveId });
            }

            try
            {
                leave.Status = "Rejected";
                leave.Remarks = remarks; // Save admin remarks

                // Create notification for student
                var notificationCount = await _context.Notifications.CountAsync();
                var notification = new Notification
                {
                    NotificationId = $"NOTIF{(notificationCount + 1):D5}",
                    UserId = leave.UserId,
                    Description = $"Your leave application from {leave.StartDate:dd MMM yyyy} to {leave.EndDate:dd MMM yyyy} has been rejected." + 
                                  (string.IsNullOrEmpty(remarks) ? "" : $" Reason: {remarks}"),
                    Status = "unread",
                    CreatedDate = DateTime.Now
                };
                _context.Notifications.Add(notification);

                await _context.SaveChangesAsync();

                // Send email notification
                try
                {
                    var mailMessage = new System.Net.Mail.MailMessage
                    {
                        To = { leave.User.Email },
                        Subject = "Leave Application Rejected - Tuition Attendance System",
                        Body = $@"
                            <html>
                              <body style='font-family: Arial, sans-serif;'>
                                <div style='max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f8f9fa;'>
                                  <div style='background-color: white; padding: 30px; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1)'>
                                    <h2 style='color: #dc3545;'>Leave Application Rejected</h2>
                                    <p>Dear <strong>{leave.User.FullName}</strong>,</p>
                                    <p>We regret to inform you that your leave application has been <strong style='color: #dc3545;'>rejected</strong>.</p>
                                    
                                    <div style='background-color: #f8f9fa; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                                      <p style='margin: 5px 0;'><strong>Leave Period:</strong> {leave.StartDate:dd MMM yyyy} to {leave.EndDate:dd MMM yyyy}</p>
                                      <p style='margin: 5px 0;'><strong>Total Days:</strong> {leave.TotalDays} day(s)</p>
                                      {(string.IsNullOrEmpty(remarks) ? "" : $"<p style='margin: 5px 0;'><strong>Reason for Rejection:</strong> {remarks}</p>")}
                                    </div>

                                    <p>If you have any questions, please contact the administration office.</p>
                                    <p><em>You may reapply for leave for the same dates if needed.</em></p>

                                    <p style='color: #6c757d; font-size: 12px; margin-top: 30px;'>
                                      This is an automated email from the Tuition Attendance System.
                                    </p>
                                  </div>
                                </div>
                              </body>
                            </html>",
                        IsBodyHtml = true
                    };
                    _helper.SendEmail(mailMessage);
                }
                catch (Exception emailEx)
                {
                    Console.WriteLine($"Warning: Failed to send email: {emailEx.Message}");
                }

                TempData["SuccessMessage"] = $"Leave application for {leave.User.FullName} has been rejected.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error rejecting leave: {ex.Message}";
            }

            return RedirectToAction(nameof(LeaveDetails), new { id = leaveId });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LeaveDelete(string id)
        {
            try
            {
                var leave = await _context.LeaveApplications
                    .Include(l => l.User)
                    .FirstOrDefaultAsync(l => l.LeaveId == id);

                if (leave != null)
                {
                    var studentName = leave.User.FullName;

                    // Delete documents from S3 if they exist
                    if (!string.IsNullOrEmpty(leave.DocumentPaths))
                    {
                        try
                        {
                            var documentUrls = JsonSerializer.Deserialize<List<string>>(leave.DocumentPaths) ?? new List<string>();
                            foreach (var docUrl in documentUrls)
                            {
                                await _s3Service.DeleteFileAsync(docUrl);
                            }
                        }
                        catch (Exception deleteEx)
                        {
                            Console.WriteLine($"Warning: Failed to delete documents: {deleteEx.Message}");
                        }
                    }

                    _context.LeaveApplications.Remove(leave);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Leave application for {studentName} deleted successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Leave application not found.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting leave application: {ex.Message}";
            }

            return RedirectToAction(nameof(LeaveIndex));
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
            var userEmail = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);

            if (user == null) return RedirectToAction("Login", "Account");

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

                    var documentUrls = new List<string>();

                    // Upload documents to S3 if provided
                    if (documents != null && documents.Any())
                    {
                        foreach (var doc in documents.Take(3)) // Max 3 files
                        {
                            if (doc.Length > 0 && doc.Length <= 5 * 1024 * 1024) // 5MB max
                            {
                                var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx" };
                                var extension = Path.GetExtension(doc.FileName).ToLower();

                                if (allowedExtensions.Contains(extension))
                                {
                                    try
                                    {
                                        var s3Path = await _s3Service.UploadDocumentAsync(doc, $"leave_documents/{leaveId}");
                                        documentUrls.Add(s3Path);
                                    }
                                    catch (Exception uploadEx)
                                    {
                                        Console.WriteLine($"Warning: Failed to upload document: {uploadEx.Message}");
                                        // Continue with other documents even if one fails
                                    }
                                }
                            }
                        }
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

                    _context.LeaveApplications.Add(leave);
                    await _context.SaveChangesAsync();

                    // Send notification to admin
                    var adminUsers = await _context.Users
                        .Where(u => u.UserType == "Admin")
                        .ToListAsync();

                    var notificationCount = await _context.Notifications.CountAsync();
                    foreach (var admin in adminUsers)
                    {
                        notificationCount++;
                        var notification = new Notification
                        {
                            NotificationId = $"NOTIF{notificationCount:D5}",
                            UserId = admin.UserId,
                            Description = $"New leave application from {user.FullName} for {totalDays} day(s) ({startDate:dd MMM} - {endDate:dd MMM})",
                            Status = "unread",
                            CreatedDate = DateTime.Now
                        };
                        _context.Notifications.Add(notification);
                    }
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Leave application submitted successfully!";
                    return RedirectToAction(nameof(StudentLeaveIndex));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error: {ex.Message}");
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
