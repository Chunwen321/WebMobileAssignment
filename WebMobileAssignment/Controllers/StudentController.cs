<<<<<<< Updated upstream
﻿using Microsoft.AspNetCore.Mvc;
=======
﻿using Microsoft.AspNetCore.Identity;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebMobileAssignment.Models;
using System.Security.Claims;
>>>>>>> Stashed changes

namespace WebMobileAssignment.Controllers
{
    public class StudentController : Controller
    {
        // Dashboard
        public IActionResult StudDashboard()
        {
            ViewBag.ActiveMenu = "Dashboard";
            return View("StudDashboard");
        }

        // Attendance History
<<<<<<< Updated upstream
        public IActionResult StudAttendanceHistory()
        {
            ViewBag.ActiveMenu = "Attendance";
            ViewBag.ActiveSubmenu = "History";
            return View("StudAttendanceHistory");
=======
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
>>>>>>> Stashed changes
        }

        // Take Attendance
        public IActionResult StudTakeAttendance()
        {
            ViewBag.ActiveMenu = "Attendance";
            ViewBag.ActiveSubmenu = "TakeAttendance";
            return View("StudTakeAttendance");
        }

        // Classes
        public IActionResult StudClasses()
        {
            ViewBag.ActiveMenu = "Classes";
            return View("StudClasses");
        }

        // Class Detail
        public IActionResult StudClassDetail()
        {
            ViewBag.ActiveMenu = "Classes";
            return View("StudClassDetail");
        }

        // Student Profile
        public IActionResult StudProfile()
        {
            ViewBag.ActiveMenu = "Profile";
            return View("StudProfile");
        }

        // Change Password
        public IActionResult StudChangePassword()
        {
            ViewBag.ActiveMenu = "Settings";
            ViewBag.ActiveSubmenu = "ChangePassword";
            return View("StudChangePassword");
        }

        // DTO for change password form
        public class ChangePasswordModel
        {
            public string CurrentPassword { get; set; } = string.Empty;
            public string NewPassword { get; set; } = string.Empty;
            public string ConfirmPassword { get; set; } = string.Empty;
        }

        // POST: Change Password
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StudChangePassword([FromForm] ChangePasswordModel model)
        {
            // Basic server-side validation
            if (string.IsNullOrWhiteSpace(model.CurrentPassword) ||
                string.IsNullOrWhiteSpace(model.NewPassword) ||
                string.IsNullOrWhiteSpace(model.ConfirmPassword))
            {
                ModelState.AddModelError(string.Empty, "All fields are required.");
                return View("StudChangePassword");
            }

            if (model.NewPassword != model.ConfirmPassword)
            {
                ModelState.AddModelError(nameof(model.ConfirmPassword), "New password and confirmation do not match.");
                return View("StudChangePassword");
            }

            // Example password complexity: min 8, at least one upper, one lower, one digit
            var passwordRegex = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$");
            if (!passwordRegex.IsMatch(model.NewPassword))
            {
                ModelState.AddModelError(nameof(model.NewPassword), "Password must be at least 8 characters and include upper-case, lower-case and a digit.");
                return View("StudChangePassword");
            }

            var student = await GetCurrentStudent();
            if (student == null)
                return RedirectToAction("Login", "Account");

            var user = student.User;
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Unable to find user record.");
                return View("StudChangePassword");
            }

            var ph = new PasswordHasher<object>();

            // Verify current password
            var verify = ph.VerifyHashedPassword(null, user.PasswordHash, model.CurrentPassword);
            if (verify != PasswordVerificationResult.Success)
            {
                ModelState.AddModelError(nameof(model.CurrentPassword), "Current password is incorrect.");
                return View("StudChangePassword");
            }

            // Prevent reuse of same password
            var verifyNewAgainstOld = ph.VerifyHashedPassword(null, user.PasswordHash, model.NewPassword);
            if (verifyNewAgainstOld == PasswordVerificationResult.Success)
            {
                ModelState.AddModelError(nameof(model.NewPassword), "New password must be different from the current password.");
                return View("StudChangePassword");
            }

            // Hash and save new password
            user.PasswordHash = ph.HashPassword(null, model.NewPassword);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Password changed successfully.";
            return RedirectToAction("StudSettings");
        }

        // GET: show apply form
        public async Task<IActionResult> StudApplyMedicalLeave()
        {
            var student = await GetCurrentStudent();
            if (student == null)
                return RedirectToAction("Login", "Account");

            ViewBag.ActiveMenu = "Attendance";
            ViewBag.ActiveSubmenu = "ApplyMedicalLeave";
            return View("StudApplyMedicalLeave", student);
        }

        // POST: apply medical leave (AJAX). Accepts JSON body.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StudApplyMedicalLeave([FromBody] ApplyMedicalLeaveRequest req)
        {
            try
            {
                var student = await GetCurrentStudent();
                if (student == null)
                    return Json(new { success = false, message = "Not authenticated" });

                if (string.IsNullOrWhiteSpace(req.ClassId))
                    return Json(new { success = false, message = "Class is required" });

                if (req.StartDate == default || req.EndDate == default)
                    return Json(new { success = false, message = "Invalid dates" });

                if (req.StartDate > req.EndDate)
                    return Json(new { success = false, message = "Start date cannot be after end date" });

                // Ensure student enrolled in class
                var enrolled = student.Enrollments.Any(e => e.ClassId == req.ClassId);
                if (!enrolled)
                    return Json(new { success = false, message = "You are not enrolled in the selected class" });

                var created = 0;
                var skipped = 0;

                for (var dt = req.StartDate.Date; dt <= req.EndDate.Date; dt = dt.AddDays(1))
                {
                    // Optionally skip weekends here if desired
                    var exists = await _context.Attendances
                        .FirstOrDefaultAsync(a => a.StudentId == student.StudentId && a.ClassId == req.ClassId && a.Date.Date == dt);

                    if (exists != null)
                    {
                        skipped++;
                        continue;
                    }

                    var attId = IdGenerator.GenerateAttendanceId(_context);

                    var attendance = new Attendance
                    {
                        AttendanceId = attId,
                        StudentId = student.StudentId,
                        ClassId = req.ClassId,
                        Date = dt,
                        TakenOn = DateTime.Now,
                        Status = "Leave" // marks as medical leave / approved leave
                    };

                    _context.Attendances.Add(attendance);
                    created++;
                }

                await _context.SaveChangesAsync();

                var msg = $"Medical leave applied. Created: {created}. Skipped (existing): {skipped}.";
                return Json(new { success = true, message = msg });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // DTO used by the POST handler
        public class ApplyMedicalLeaveRequest
        {
            public string ClassId { get; set; } = string.Empty;
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public string? Reason { get; set; }
        }

        // Nested DTO inside StudentController to bind multipart/form-data
        public class ApplyMedicalLeaveForm
        {
            public string ClassId { get; set; } = string.Empty;
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public string? Reason { get; set; }
            public Microsoft.AspNetCore.Http.IFormFile? Photo { get; set; }
        }

        // POST: apply medical leave with file upload (AJAX). Accepts multipart/form-data.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StudApplyMedicalLeave([FromForm] ApplyMedicalLeaveForm req)
        {
            try
            {
                var student = await GetCurrentStudent();
                if (student == null)
                    return Json(new { success = false, message = "Not authenticated" });

                if (string.IsNullOrWhiteSpace(req.ClassId))
                    return Json(new { success = false, message = "Class is required" });

                if (req.StartDate == default || req.EndDate == default)
                    return Json(new { success = false, message = "Invalid dates" });

                if (req.StartDate > req.EndDate)
                    return Json(new { success = false, message = "Start date cannot be after end date" });

                // Ensure student enrolled in class
                var enrolled = student.Enrollments.Any(e => e.ClassId == req.ClassId);
                if (!enrolled)
                    return Json(new { success = false, message = "You are not enrolled in the selected class" });

                // Save uploaded photo (if any)
                string? savedFilePath = null;
                if (req.Photo != null && req.Photo.Length > 0)
                {
                    // Optional: validate content type and size (example: max 5 MB)
                    var permitted = new[] { "image/jpeg", "image/png", "image/jpg" };
                    if (!permitted.Contains(req.Photo.ContentType))
                    {
                        return Json(new { success = false, message = "Invalid file type. Only jpg/png allowed." });
                    }
                    const long maxBytes = 5 * 1024 * 1024; // 5 MB
                    if (req.Photo.Length > maxBytes)
                    {
                        return Json(new { success = false, message = "File too large. Max 5 MB allowed." });
                    }

                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "medical-leave");
                    Directory.CreateDirectory(uploadsFolder);

                    var ext = Path.GetExtension(req.Photo.FileName);
                    var fileName = $"{Guid.NewGuid()}{ext}";
                    var filePath = Path.Combine(uploadsFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await req.Photo.CopyToAsync(stream);
                    }

                    // path relative to web root (for later use/display)
                    savedFilePath = $"/uploads/medical-leave/{fileName}";
                }

                var created = 0;
                var skipped = 0;

                for (var dt = req.StartDate.Date; dt <= req.EndDate.Date; dt = dt.AddDays(1))
                {
                    // Optional: skip weekends if needed
                    // if (dt.DayOfWeek == DayOfWeek.Saturday || dt.DayOfWeek == DayOfWeek.Sunday) continue;

                    var exists = await _context.Attendances
                        .FirstOrDefaultAsync(a => a.StudentId == student.StudentId && a.ClassId == req.ClassId && a.Date.Date == dt);

                    if (exists != null)
                    {
                        skipped++;
                        continue;
                    }

                    var attId = IdGenerator.GenerateAttendanceId(_context);

                    var attendance = new Attendance
                    {
                        AttendanceId = attId,
                        StudentId = student.StudentId,
                        ClassId = req.ClassId,
                        Date = dt,
                        TakenOn = DateTime.Now,
                        Status = "Leave"
                        // If you later add a PhotoPath column to Attendance, set it here using savedFilePath
                    };

                    _context.Attendances.Add(attendance);
                    created++;
                }

                await _context.SaveChangesAsync();

                var msg = $"Medical leave applied. Created: {created}. Skipped (existing): {skipped}.";
                if (savedFilePath != null)
                {
                    msg += " Photo uploaded.";
                    // Optionally store the savedFilePath in a related table or notification.
                }

                return Json(new { success = true, message = msg });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: Settings (load current student and pass model to view)
public async Task<IActionResult> StudSettings()
{
    var student = await GetCurrentStudent();
    if (student == null)
        return RedirectToAction("Login", "Account");

    ViewBag.ActiveMenu = "Settings";
    ViewBag.ActiveSubmenu = "Settings";
    return View("StudSettings", student);
}

// DTO for binding posted settings (keeps view/controller decoupled)
public class StudentSettingsModel
{
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Address { get; set; }
}

// POST: save settings
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> StudSettings([FromForm] StudentSettingsModel model)
{
    var student = await GetCurrentStudent();
    if (student == null)
        return RedirectToAction("Login", "Account");

    // Update navigation User entity safely (only update fields allowed)
    if (!string.IsNullOrWhiteSpace(model.FullName))
        student.User.FullName = model.FullName.Trim();

    if (!string.IsNullOrWhiteSpace(model.Email))
        student.User.Email = model.Email.Trim();

    if (!string.IsNullOrWhiteSpace(model.PhoneNumber))
        student.User.PhoneNumber = model.PhoneNumber.Trim();

    if (model.DateOfBirth.HasValue)
        student.User.DateOfBirth = model.DateOfBirth.Value;

    // If you have an Address property on Student or User, update it here.
    // Example: student.Address = model.Address;

    // Save changes (User is tracked via student navigation)
    await _context.SaveChangesAsync();

    TempData["SuccessMessage"] = "Profile updated successfully.";
    return RedirectToAction("StudSettings");
}
    }
}
