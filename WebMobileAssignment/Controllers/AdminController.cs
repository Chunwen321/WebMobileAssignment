using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebMobileAssignment.Models;
using WebMobileAssignment.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace WebMobileAssignment.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly DB _context;
        private readonly Helper _helper;
        private readonly S3Service _s3Service;

        public AdminController(DB context, Helper helper, S3Service s3Service)
        {
            _context = context;
          _helper = helper;
          _s3Service = s3Service;
        }

        // ==================== DASHBOARD ====================
        public async Task<IActionResult> Dashboard()
        {
            ViewBag.ActiveMenu = "Dashboard";
            ViewBag.Title = "Dashboard";

            var totalStudents = await _context.Students.CountAsync();
            var totalTeachers = await _context.Teachers.CountAsync();
            var totalClasses = await _context.Classes.CountAsync();
            var totalParents = await _context.Parents.CountAsync();

            var attendanceToday = await _context.Attendances
                .Where(a => a.Date.Date == DateTime.Today)
                .CountAsync();

            // Count both "Present" and "Leave" as present
            var presentToday = await _context.Attendances
                .Where(a => a.Date.Date == DateTime.Today && (a.Status == "Present" || a.Status == "Leave"))
                .CountAsync();

            var absentToday = await _context.Attendances
                .Where(a => a.Date.Date == DateTime.Today && a.Status == "Absent")
                .CountAsync();

            var lateToday = await _context.Attendances
                .Where(a => a.Date.Date == DateTime.Today && a.Status == "Late")
                .CountAsync();

            var recentAttendance = await _context.Attendances
                .Include(a => a.Student)
                .ThenInclude(s => s.User)
                .Include(a => a.Class)
                .OrderByDescending(a => a.Date)
                .Take(10)
                .ToListAsync();

            ViewBag.TotalStudents = totalStudents;
            ViewBag.TotalTeachers = totalTeachers;
            ViewBag.TotalClasses = totalClasses;
            ViewBag.TotalParents = totalParents;
            ViewBag.AttendanceToday = attendanceToday;
            ViewBag.PresentToday = presentToday;
            ViewBag.AbsentToday = absentToday;
            ViewBag.LateToday = lateToday;

            return View(recentAttendance);
        }

        // ==================== STUDENT MANAGEMENT ====================
        public async Task<IActionResult> StudentIndex()
        {
            ViewBag.ActiveMenu = "StudentManagement";
            ViewBag.Title = "Student Management";

            var students = await _context.Students
                .Include(s => s.User)
                .Include(s => s.Parent)
                    .ThenInclude(p => p.User)
                .Include(s => s.Enrollments)
                    .ThenInclude(e => e.Class)
                .OrderBy(s => s.StudentId)
                .ToListAsync();

            return View(students);
        }

        public async Task<IActionResult> AddStudent()
        {
            ViewBag.ActiveMenu = "StudentManagement";
            ViewBag.Title = "Add New Student";
            ViewBag.Parents = await _context.Parents.Include(p => p.User).ToListAsync();
            ViewBag.Classes = await _context.Classes.ToListAsync();

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> FindParentByEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return Json(new { success = false });

            var parent = await _context.Parents.Include(p => p.User)
                .FirstOrDefaultAsync(p => p.User.Email.ToLower() == email.ToLower());

            if (parent == null)
                return Json(new { success = false });

            return Json(new
            {
                success = true,
                parentId = parent.ParentId,
                fullName = parent.User.FullName,
                email = parent.User.Email,
                phone = parent.User.PhoneNumber ?? "",
                address = parent.Address ?? "",
                dob = parent.User.DateOfBirth?.ToString("yyyy-MM-dd") ?? "",
                gender = parent.User.Gender ?? ""
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetParentById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return Json(new { success = false });

            var parent = await _context.Parents
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.ParentId == id);

            if (parent == null)
                return Json(new { success = false });

            return Json(new
            {
                success = true,
                parentId = parent.ParentId,
                fullName = parent.User.FullName,
                email = parent.User.Email,
                phone = parent.User.PhoneNumber ?? "",
                address = parent.Address ?? "",
                dob = parent.User.DateOfBirth?.ToString("yyyy-MM-dd") ?? "",
                gender = parent.User.Gender ?? ""
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StudentCreate(string fullName, string email,
            string parentId, List<string>? classIds, DateTime? dateOfBirth, string gender,
            string? phoneNumber, string status, bool isActive, DateTime? enrollmentDate,
            // New parent fields - matching database columns
            string newParentFullName, string newParentEmail, string newParentPhone, 
            string newParentAddress, DateTime? newParentDateOfBirth, string newParentGender,
            // Profile picture uploads
            IFormFile? profilePicture,
            IFormFile? parentProfilePicture)
        {
            // If parentId is empty and newParentEmail provided, we'll create a parent
            if (string.IsNullOrEmpty(parentId) && !string.IsNullOrWhiteSpace(newParentEmail))
            {
                // check if parent already exists
                var existing = await _context.Parents.Include(p => p.User)
                    .FirstOrDefaultAsync(p => p.User.Email.ToLower() == newParentEmail.ToLower());
                if (existing != null)
                {
                    parentId = existing.ParentId;
                }
            }

            // Clear ModelState for optional fields that might be empty strings
            if (string.IsNullOrEmpty(parentId))
            {
                ModelState.Remove("parentId");
                parentId = null;
            }

            if (classIds == null || !classIds.Any())
            {
                ModelState.Remove("classIds");
                classIds = new List<string>();
            }

            // Remove validation for optional profile pictures
            ModelState.Remove("profilePicture");
            ModelState.Remove("parentProfilePicture");

            // Manual validation for required fields
            if (string.IsNullOrWhiteSpace(fullName))
                ModelState.AddModelError("fullName", "Full name is required");

            if (string.IsNullOrWhiteSpace(email))
                ModelState.AddModelError("email", "Email is required");

            if (string.IsNullOrWhiteSpace(gender))
                ModelState.AddModelError("gender", "Gender is required");

            if (!dateOfBirth.HasValue || dateOfBirth.Value == default(DateTime))
                ModelState.AddModelError("dateOfBirth", "Date of birth is required");

            if (string.IsNullOrWhiteSpace(status))
            {
                status = "active"; // Default value
            }

            // Validate new parent fields if creating a new parent
            if (string.IsNullOrEmpty(parentId) && !string.IsNullOrWhiteSpace(newParentEmail))
            {
                if (string.IsNullOrWhiteSpace(newParentFullName))
                    ModelState.AddModelError("newParentFullName", "Parent full name is required when creating a new parent");
                
                // Remove validation for optional parent fields
                ModelState.Remove("newParentPhone");
                ModelState.Remove("newParentAddress");
                ModelState.Remove("newParentDateOfBirth");
                ModelState.Remove("newParentGender");
            }
            else
            {
                // If we have a parentId (existing parent), remove validation errors for new parent fields
                ModelState.Remove("newParentFullName");
                ModelState.Remove("newParentEmail");
                ModelState.Remove("newParentPhone");
                ModelState.Remove("newParentAddress");
                ModelState.Remove("newParentDateOfBirth");
                ModelState.Remove("newParentGender");
            }

            // Validate class capacity
            if (classIds != null && classIds.Any())
            {
                var selectedClasses = await _context.Classes
                    .Where(c => classIds.Contains(c.ClassId))
                    .ToListAsync();

                var capacityErrors = new List<string>();
                foreach (var cls in selectedClasses)
                {
                    if (cls.CurrentCapacity >= cls.MaxCapacity)
                    {
                        capacityErrors.Add($"{cls.ClassName} is full ({cls.CurrentCapacity}/{cls.MaxCapacity})");
                    }
                }

                if (capacityErrors.Any())
                {
                    foreach (var error in capacityErrors)
                        ModelState.AddModelError("classIds", error);
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Generate random temporary password for new user
                    var temporaryPassword = _helper.RandomPassword();

                    // If need to create new parent
                    if (string.IsNullOrEmpty(parentId) && !string.IsNullOrWhiteSpace(newParentEmail))
                    {
                        // create parent user and parent with all fields using IdGenerator
                        var parentUserId = IdGenerator.GenerateUserId(_context);
                        var parentIdGen = IdGenerator.GenerateParentId(_context);

                        var parentTempPassword = _helper.RandomPassword();

                        // Handle parent profile picture upload to S3
                        string? parentProfilePictureUrl = null;
                        if (parentProfilePicture != null && parentProfilePicture.Length > 0)
                        {
                            try
                            {
                                parentProfilePictureUrl = await _s3Service.UploadFileAsync(parentProfilePicture, parentUserId);
                            }
                            catch (Exception uploadEx)
                            {
                                Console.WriteLine($"Warning: Failed to upload parent profile picture: {uploadEx.Message}");
                                // Don't fail the operation if upload fails - use default
                            }
                        }

                        var parentUser = new User
                        {
                            UserId = parentUserId,
                            FullName = newParentFullName,
                            Email = newParentEmail,
                            PasswordHash = _helper.HashPassword(parentTempPassword),
                            DateOfBirth = newParentDateOfBirth, // Include date of birth
                            Gender = newParentGender, // Include gender
                            ProfilePicture = parentProfilePictureUrl, // Set parent profile picture URL or leave null for default
                            UserType = "Parent",
                            CreatedDate = DateTime.Now,
                            Status = "active",
                            IsActive = true
                        };
                        _context.Users.Add(parentUser);

                        var parent = new Parent
                        {
                            ParentId = parentIdGen,
                            UserId = parentUserId,
                            Address = newParentAddress // Store address in Parent table
                        };
                        _context.Parents.Add(parent);

                        // Save to get parent in DB
                        await _context.SaveChangesAsync();

                        // Send welcome email to new parent
                        try
                        {
                            _helper.SendWelcomeEmail(newParentEmail, newParentFullName, "Parent", parentTempPassword);
                        }
                        catch (Exception emailEx)
                        {
                            Console.WriteLine($"Warning: Failed to send welcome email to {newParentEmail}: {emailEx.Message}");
                            // Don't fail the operation if email fails
                        }

                        parentId = parent.ParentId;
                    }

                    // Generate IDs for student using IdGenerator
                    var userId = IdGenerator.GenerateUserId(_context);
                    var studentId = IdGenerator.GenerateStudentId(_context);

                    // Handle student profile picture upload to S3
                    string? profilePictureUrl = null;
                    if (profilePicture != null && profilePicture.Length > 0)
                    {
                        try
                        {
                            profilePictureUrl = await _s3Service.UploadFileAsync(profilePicture, userId);
                        }
                        catch (Exception uploadEx)
                        {
                            Console.WriteLine($"Warning: Failed to upload profile picture: {uploadEx.Message}");
                            // Don't fail the operation if upload fails - use default
                        }
                    }

                    // Create User with all fields - using random temporary password
                    var user = new User
                    {
                        UserId = userId,
                        FullName = fullName,
                        Email = email,
                        PasswordHash = _helper.HashPassword(temporaryPassword),
                        PhoneNumber = phoneNumber,
                        DateOfBirth = dateOfBirth,
                        Gender = gender,
                        ProfilePicture = profilePictureUrl, // Set profile picture URL or leave null for default icon
                        UserType = "Student",
                        CreatedDate = DateTime.Now,
                        Status = status,
                        IsActive = true
                    };
                    _context.Users.Add(user);

                    // Create Student with all fields
                    var student = new Student
                    {
                        StudentId = studentId,
                        UserId = userId,
                        ParentId = parentId,
                        DateOfBirth = dateOfBirth.Value,
                        Gender = gender,
                        EnrollmentDate = enrollmentDate ?? DateTime.Now
                    };
                    _context.Students.Add(student);

                    await _context.SaveChangesAsync();

                    // Send welcome email to student
                    try
                    {
                        _helper.SendWelcomeEmail(email, fullName, "Student", temporaryPassword);
                    }
                    catch (Exception emailEx)
                    {
                        Console.WriteLine($"Warning: Failed to send welcome email to {email}: {emailEx.Message}");
                        // Don't fail the operation if email fails
                    }

                    // Create enrollment entries for all selected classes and update capacity
                    int enrolledCount = 0;
                    if (classIds != null && classIds.Any())
                    {
                        foreach (var classId in classIds)
                        {
                            // Create enrollment
                            var enrollment = new Enrollment
                            {
                                StudentId = studentId,
                                ClassId = classId,
                                EnrolledDate = DateTime.Now
                            };
                            _context.Enrollments.Add(enrollment);

                            // Update class current capacity
                            var classToUpdate = await _context.Classes.FindAsync(classId);
                            if (classToUpdate != null)
                            {
                                classToUpdate.CurrentCapacity++;
                            }

                            enrolledCount++;
                        }
                        await _context.SaveChangesAsync();
                    }

                    TempData["SuccessMessage"] = $"Student '{fullName}' added successfully with {enrolledCount} class enrollment(s)! A temporary password has been sent to {email}.";

                    return RedirectToAction(nameof(StudentIndex));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error saving student: {ex.Message}");
                }
            }

            // If we got here, something failed - reload the form with data
            ViewBag.ActiveMenu = "StudentManagement";
            ViewBag.Title = "Add New Student";
            ViewBag.FullName = fullName;
            ViewBag.Email = email;
            ViewBag.PhoneNumber = phoneNumber;
            ViewBag.DateOfBirth = dateOfBirth?.ToString("yyyy-MM-dd");
            ViewBag.Gender = gender;
            ViewBag.Status = status;
            ViewBag.IsActive = isActive;
            ViewBag.EnrollmentDate = enrollmentDate?.ToString("yyyy-MM-dd");
            ViewBag.ParentId = parentId;
            ViewBag.ClassIds = classIds;
            ViewBag.Parents = await _context.Parents.Include(p => p.User).ToListAsync();
            ViewBag.Classes = await _context.Classes.ToListAsync();
            return View("AddStudent");
        }

        public async Task<IActionResult> StudentEdit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var student = await _context.Students
                .Include(s => s.User)
                .Include(s => s.Enrollments)
                    .ThenInclude(e => e.Class)
                .FirstOrDefaultAsync(s => s.StudentId == id);

            if (student == null) return NotFound();

            ViewBag.ActiveMenu = "StudentManagement";
            ViewBag.Title = "Edit Student";
            ViewBag.Parents = await _context.Parents.Include(p => p.User).ToListAsync();
            ViewBag.Classes = await _context.Classes.ToListAsync();

            return View(student);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StudentEdit(string studentId, string fullName, string email,
            string? phoneNumber, string parentId, List<string>? classIds, string? removeClassIds,
            DateTime dateOfBirth, string gender, string status, IFormFile? profilePicture)
        {
            var student = await _context.Students
                .Include(s => s.User)
                .Include(s => s.Enrollments)
                    .ThenInclude(e => e.Class)
                .FirstOrDefaultAsync(s => s.StudentId == studentId);

            if (student == null)
            {
                TempData["ErrorMessage"] = "Student not found.";
                return NotFound();
            }

            // Validate required fields
            if (string.IsNullOrWhiteSpace(fullName))
                ModelState.AddModelError("fullName", "Full name is required");

            if (string.IsNullOrWhiteSpace(email))
                ModelState.AddModelError("email", "Email is required");

            if (string.IsNullOrWhiteSpace(gender))
                ModelState.AddModelError("gender", "Gender is required");

            if (string.IsNullOrWhiteSpace(status))
                ModelState.AddModelError("status", "Status is required");

            // Remove validation for optional profile picture
            ModelState.Remove("profilePicture");

            // Validate class capacity for new enrollments
            if (classIds != null && classIds.Any())
            {
                var selectedClasses = await _context.Classes
                    .Where(c => classIds.Contains(c.ClassId))
                    .ToListAsync();

                var capacityErrors = new List<string>();
                foreach (var cls in selectedClasses)
                {
                    if (cls.CurrentCapacity >= cls.MaxCapacity)
                    {
                        capacityErrors.Add($"{cls.ClassName} is full ({cls.CurrentCapacity}/{cls.MaxCapacity})");
                    }
                }

                if (capacityErrors.Any())
                {
                    foreach (var error in capacityErrors)
                    {
                        ModelState.AddModelError("classIds", error);
                    }
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Handle profile picture upload
                    if (profilePicture != null && profilePicture.Length > 0)
                    {
                        try
                        {
                            // Delete old picture if exists
                            if (!string.IsNullOrEmpty(student.User.ProfilePicture) && 
                                !student.User.ProfilePicture.StartsWith("/images/"))
                            {
                                await _s3Service.DeleteFileAsync(student.User.ProfilePicture);
                            }
                            
                            // Upload new picture
                            var profilePictureUrl = await _s3Service.UploadFileAsync(profilePicture, student.User.UserId);
                            student.User.ProfilePicture = profilePictureUrl;
                        }
                        catch (Exception uploadEx)
                        {
                            Console.WriteLine($"Warning: Failed to upload profile picture: {uploadEx.Message}");
                        }
                    }

                    // Update user information
                    student.User.FullName = fullName;
                    student.User.Email = email;
                    student.User.PhoneNumber = phoneNumber;
                    student.User.Status = status;

                    // Update student information
                    student.ParentId = string.IsNullOrEmpty(parentId) ? null : parentId;
                    student.DateOfBirth = dateOfBirth;
                    student.Gender = gender;

                    _context.Update(student);

                    int addedCount = 0;
                    int removedCount = 0;

                    // Handle removal of enrollments
                    if (!string.IsNullOrEmpty(removeClassIds))
                    {
                        var classIdsToRemove = removeClassIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var classIdToRemove in classIdsToRemove)
                        {
                            var enrollmentToRemove = student.Enrollments
                                .FirstOrDefault(e => e.ClassId == classIdToRemove);

                            if (enrollmentToRemove != null)
                            {
                                _context.Enrollments.Remove(enrollmentToRemove);

                                // Decrease class current capacity
                                var classToUpdate = await _context.Classes.FindAsync(classIdToRemove);
                                if (classToUpdate != null && classToUpdate.CurrentCapacity > 0)
                                {
                                    classToUpdate.CurrentCapacity--;
                                }
                                removedCount++;
                            }
                        }
                    }

                    // Handle addition of new enrollments
                    if (classIds != null && classIds.Any())
                    {
                        foreach (var classId in classIds)
                        {
                            // Check if already enrolled in this class
                            var existingEnrollment = student.Enrollments
                                .FirstOrDefault(e => e.ClassId == classId);

                            if (existingEnrollment == null)
                            {
                                // Add new enrollment
                                var enrollment = new Enrollment
                                {
                                    StudentId = studentId,
                                    ClassId = classId,
                                    EnrolledDate = DateTime.Now
                                };
                                _context.Enrollments.Add(enrollment);

                                // Increase class current capacity
                                var classToUpdate = await _context.Classes.FindAsync(classId);
                                if (classToUpdate != null)
                                {
                                    classToUpdate.CurrentCapacity++;
                                }
                                addedCount++;
                            }
                        }
                    }

                    await _context.SaveChangesAsync();

                    var message = $"Student '{fullName}' updated successfully!";
                    if (addedCount > 0 || removedCount > 0)
                    {
                        message += $" Added {addedCount} enrollment(s), removed {removedCount} enrollment(s).";
                    }

                    TempData["SuccessMessage"] = message;
                    return RedirectToAction(nameof(StudentIndex));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Students.Any(e => e.StudentId == studentId))
                    {
                        TempData["ErrorMessage"] = "Student not found. It may have been deleted.";
                        return NotFound();
                    }
                    TempData["ErrorMessage"] = "Unable to save changes. The student was modified by another user.";
                    throw;
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Error updating student: {ex.Message}";
                }
            }

            ViewBag.Parents = await _context.Parents.Include(p => p.User).ToListAsync();
            ViewBag.Classes = await _context.Classes.ToListAsync();
            return View(student);
        }

        public async Task<IActionResult> StudentDetails(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var student = await _context.Students
                .Include(s => s.User)
                .Include(s => s.Parent)
                    .ThenInclude(p => p.User)
                .Include(s => s.Enrollments)
                    .ThenInclude(e => e.Class)
                    .ThenInclude(c => c.Teacher)
                    .ThenInclude(t => t.User)
                .Include(s => s.Enrollments)
                    .ThenInclude(e => e.Class)
                    .ThenInclude(c => c.Subject)
                .Include(s => s.Attendances)
                .FirstOrDefaultAsync(m => m.StudentId == id);

            if (student == null) return NotFound();

            // Get attendance statistics - Count "Leave" as present
            var attendanceStats = await _context.Attendances
                .Where(a => a.StudentId == id)
                .GroupBy(a => a.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var totalAttendance = attendanceStats.Sum(s => s.Count);
            var presentCount = attendanceStats.FirstOrDefault(s => s.Status == "Present")?.Count ?? 0;
            var leaveCount = attendanceStats.FirstOrDefault(s => s.Status == "Leave")?.Count ?? 0;
            var absentCount = attendanceStats.FirstOrDefault(s => s.Status == "Absent")?.Count ?? 0;
            var lateCount = attendanceStats.FirstOrDefault(s => s.Status == "Late")?.Count ?? 0;
            
            // Count both Present and Leave as present for attendance rate
            var totalPresentIncludingLeave = presentCount + leaveCount;
            var attendanceRate = totalAttendance > 0 ? Math.Round((decimal)totalPresentIncludingLeave / totalAttendance * 100, 1) : 0;

            ViewBag.TotalEnrollments = student.Enrollments?.Count ?? 0;
            ViewBag.TotalAttendance = totalAttendance;
            ViewBag.PresentCount = totalPresentIncludingLeave; // Show combined count
            ViewBag.AbsentCount = absentCount;
            ViewBag.LateCount = lateCount;
            ViewBag.AttendanceRate = attendanceRate;
            ViewBag.YearsSinceEnrollment = student.EnrollmentDate.HasValue
                ? Math.Round((DateTime.Now - student.EnrollmentDate.Value).TotalDays / 365.25, 1)
                : 0;
            ViewBag.Age = student.DateOfBirth.HasValue
                ? DateTime.Now.Year - student.DateOfBirth.Value.Year
                : 0;

            ViewBag.ActiveMenu = "StudentManagement";
            ViewBag.Title = "Student Details";

            return View(student);
        }

        public async Task<IActionResult> StudentDelete(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var student = await _context.Students
                .Include(s => s.User)
                .Include(s => s.Parent)
                .ThenInclude(p => p.User)
                .FirstOrDefaultAsync(m => m.StudentId == id);

            if (student == null) return NotFound();

            ViewBag.ActiveMenu = "StudentManagement";
            ViewBag.Title = "Delete Student";

            return View(student);
        }

        [HttpPost, ActionName("StudentDelete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StudentDeleteConfirmed(string id)
        {
            try
            {
                var student = await _context.Students
                    .Include(s => s.User)
                    .Include(s => s.Enrollments)
                    .FirstOrDefaultAsync(s => s.StudentId == id);

                if (student != null)
                {
                    var studentName = student.User.FullName;

                    // Decrease capacity for all enrolled classes before deletion
                    if (student.Enrollments != null && student.Enrollments.Any())
                    {
                        foreach (var enrollment in student.Enrollments)
                        {
                            var classToUpdate = await _context.Classes.FindAsync(enrollment.ClassId);
                            if (classToUpdate != null && classToUpdate.CurrentCapacity > 0)
                            {
                                classToUpdate.CurrentCapacity--;
                            }
                        }
                    }

                    _context.Users.Remove(student.User); // Cascade delete will remove student and enrollments
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Student '{studentName}' deleted successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Student not found. It may have already been deleted.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting student: {ex.Message}";
            }

            return RedirectToAction(nameof(StudentIndex));
        }

        // ==================== TEACHER MANAGEMENT ====================
        public async Task<IActionResult> TeacherIndex()
        {
            ViewBag.ActiveMenu = "TeacherManagement";
            ViewBag.Title = "Teacher Management";

            var teachers = await _context.Teachers.Include(t => t.User).ToListAsync();
            return View(teachers);
        }

        public IActionResult TeacherCreate()
        {
            ViewBag.ActiveMenu = "TeacherManagement";
            ViewBag.Title = "Create Teacher";

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TeacherCreate(
            string fullName, string email,
            string? phoneNumber, string? subjectTeach, DateTime? hireDate,
            string? title, string? education, string? skill, string? bio,
            DateTime? dateOfBirth, string? gender, string? status,
            IFormFile? profilePicture)
        {
            // Manual validation for required fields
            if (string.IsNullOrWhiteSpace(fullName))
                ModelState.AddModelError("fullName", "Full name is required");

            if (string.IsNullOrWhiteSpace(email))
                ModelState.AddModelError("email", "Email is required");

            if (!hireDate.HasValue)
                ModelState.AddModelError("hireDate", "Hire date is required");

            // Set default status if not provided
            if (string.IsNullOrWhiteSpace(status))
            {
                status = "active";
            }

            // Remove validation for optional profile picture
            ModelState.Remove("profilePicture");

            if (ModelState.IsValid)
            {
                try
                {
                    // Generate random temporary password for new teacher
                    var temporaryPassword = _helper.RandomPassword();

                    // Generate IDs using IdGenerator
                    var userId = IdGenerator.GenerateUserId(_context);
                    var teacherId = IdGenerator.GenerateTeacherId(_context);

                    // Handle profile picture upload to S3
                    string? profilePictureUrl = null;
                    if (profilePicture != null && profilePicture.Length > 0)
                    {
                        try
                        {
                            profilePictureUrl = await _s3Service.UploadFileAsync(profilePicture, userId);
                        }
                        catch (Exception uploadEx)
                        {
                            Console.WriteLine($"Warning: Failed to upload profile picture: {uploadEx.Message}");
                            // Don't fail the operation if upload fails - use default
                        }
                    }

                    // Create User with all fields including optional ones - using random temporary password
                    var user = new User
                    {
                        UserId = userId,
                        FullName = fullName,
                        Email = email,
                        PasswordHash = _helper.HashPassword(temporaryPassword),
                        PhoneNumber = phoneNumber,
                        DateOfBirth = dateOfBirth,
                        Gender = gender,
                        ProfilePicture = profilePictureUrl, // Set profile picture URL or leave null for default icon
                        UserType = "Teacher",
                        CreatedDate = DateTime.Now,
                        Status = status,
                        IsActive = status == "active"
                    };
                    _context.Users.Add(user);

                    // Create Teacher with all professional fields
                    var teacher = new Teacher
                    {
                        TeacherId = teacherId,
                        UserId = userId,
                        PhoneNumber = phoneNumber,
                        SubjectTeach = subjectTeach,
                        HireDate = hireDate.Value,
                        Title = title,
                        Education = education,
                        Skill = skill,
                        Bio = bio
                    };
                    _context.Teachers.Add(teacher);

                    await _context.SaveChangesAsync();
                    
                    // Send welcome email to teacher
                    try
                    {
                        _helper.SendWelcomeEmail(email, fullName, "Teacher", temporaryPassword);
                    }
                    catch (Exception emailEx)
                    {
                        Console.WriteLine($"Warning: Failed to send welcome email to {email}: {emailEx.Message}");
                        // Don't fail the operation if email fails
                    }
                    
                    TempData["SuccessMessage"] = $"Teacher '{fullName}' added successfully! A temporary password has been sent to {email}.";
                    return RedirectToAction(nameof(TeacherIndex));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error saving teacher: {ex.Message}");
                }
            }

            // If validation failed, return view with data
            ViewBag.ActiveMenu = "TeacherManagement";
            ViewBag.Title = "Create Teacher";
            ViewBag.FullName = fullName;
            ViewBag.Email = email;
            ViewBag.PhoneNumber = phoneNumber;
            ViewBag.SubjectTeach = subjectTeach;
            ViewBag.HireDate = hireDate?.ToString("yyyy-MM-dd");
            ViewBag.Title = title;
            ViewBag.Education = education;
            ViewBag.Skill = skill;
            ViewBag.Bio = bio;
            ViewBag.DateOfBirth = dateOfBirth?.ToString("yyyy-MM-dd");
            ViewBag.Gender = gender;
            ViewBag.Status = status;

            return View();
        }

        public async Task<IActionResult> TeacherEdit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var teacher = await _context.Teachers
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TeacherId == id);

            if (teacher == null) return NotFound();

            ViewBag.ActiveMenu = "TeacherManagement";
            ViewBag.Title = "Edit Teacher";

            return View(teacher);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TeacherEdit(
            string teacherId, string fullName, string email,
            string? phoneNumber, string? subjectTeach, DateTime? hireDate,
            string? title, string? education, string? skill, string? bio,
            DateTime? dateOfBirth, string? gender, string? status,
            IFormFile? profilePicture)
        {
            var teacher = await _context.Teachers
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TeacherId == teacherId);

            if (teacher == null)
            {
                TempData["ErrorMessage"] = "Teacher not found.";
                return NotFound();
            }

            // Validate required fields
            if (string.IsNullOrWhiteSpace(fullName))
                ModelState.AddModelError("fullName", "Full name is required");

            if (string.IsNullOrWhiteSpace(email))
                ModelState.AddModelError("email", "Email is required");

            if (!hireDate.HasValue)
                ModelState.AddModelError("hireDate", "Hire date is required");

            if (string.IsNullOrWhiteSpace(status))
                ModelState.AddModelError("status", "Status is required");

            // Remove validation for optional profile picture
            ModelState.Remove("profilePicture");

            if (ModelState.IsValid)
            {
                try
                {
                    // Handle profile picture upload
                    if (profilePicture != null && profilePicture.Length > 0)
                    {
                        try
                        {
                            // Delete old picture if exists
                            if (!string.IsNullOrEmpty(teacher.User.ProfilePicture) && 
                                !teacher.User.ProfilePicture.StartsWith("/images/"))
                            {
                                await _s3Service.DeleteFileAsync(teacher.User.ProfilePicture);
                            }
                            
                            // Upload new picture
                            var profilePictureUrl = await _s3Service.UploadFileAsync(profilePicture, teacher.User.UserId);
                            teacher.User.ProfilePicture = profilePictureUrl;
                        }
                        catch (Exception uploadEx)
                        {
                            Console.WriteLine($"Warning: Failed to upload profile picture: {uploadEx.Message}");
                            // Don't fail the operation if upload fails - use default
                        }
                    }

                    // Update User information
                    teacher.User.FullName = fullName;
                    teacher.User.Email = email;
                    teacher.User.PhoneNumber = phoneNumber;
                    teacher.User.DateOfBirth = dateOfBirth;
                    teacher.User.Gender = gender;
                    teacher.User.Status = status;
                    teacher.User.IsActive = status == "active";

                    // Update Teacher professional information
                    teacher.PhoneNumber = phoneNumber;
                    teacher.SubjectTeach = subjectTeach;
                    teacher.HireDate = hireDate;
                    teacher.Title = title;
                    teacher.Education = education;
                    teacher.Skill = skill;
                    teacher.Bio = bio;

                    _context.Update(teacher);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Teacher '{fullName}' updated successfully!";
                    return RedirectToAction(nameof(TeacherIndex));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Teachers.Any(e => e.TeacherId == teacherId))
                    {
                        TempData["ErrorMessage"] = "Teacher not found. It may have been deleted.";
                        return NotFound();
                    }
                    TempData["ErrorMessage"] = "Unable to save changes. The teacher was modified by another user.";
                    throw;
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Error updating teacher: {ex.Message}";
                }
            }

            ViewBag.ActiveMenu = "TeacherManagement";
            ViewBag.Title = "Edit Teacher";
            return View(teacher);
        }

        public async Task<IActionResult> TeacherDetails(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var teacher = await _context.Teachers
                .Include(t => t.User)
                .Include(t => t.Classes)
                    .ThenInclude(c => c.Subject)
                .Include(t => t.Classes)
                    .ThenInclude(c => c.Enrollments)
                .FirstOrDefaultAsync(m => m.TeacherId == id);

            if (teacher == null) return NotFound();

            // Get attendance statistics from ALL classes taught by this teacher
            // (not just attendance records marked by this teacher)
            var classIds = teacher.Classes?.Select(c => c.ClassId).ToList() ?? new List<string>();

            var attendanceStats = await _context.Attendances
                .Where(a => classIds.Contains(a.ClassId))
                .GroupBy(a => a.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var totalAttendanceMarked = attendanceStats.Sum(s => s.Count);
            var presentCount = attendanceStats.FirstOrDefault(s => s.Status == "Present")?.Count ?? 0;
            var absentCount = attendanceStats.FirstOrDefault(s => s.Status == "Absent")?.Count ?? 0;
            var lateCount = attendanceStats.FirstOrDefault(s => s.Status == "Late")?.Count ?? 0;

            ViewBag.TotalClassesAssigned = teacher.Classes?.Count ?? 0;
            ViewBag.TotalStudentsTeaching = teacher.Classes?.Sum(c => c.CurrentCapacity) ?? 0;
            ViewBag.TotalAttendanceMarked = totalAttendanceMarked;
            ViewBag.PresentCount = presentCount;
            ViewBag.AbsentCount = absentCount;
            ViewBag.LateCount = lateCount;
            ViewBag.YearsOfService = teacher.HireDate.HasValue
                ? Math.Round((DateTime.Now - teacher.HireDate.Value).TotalDays / 365.25, 1)
                : 0;

            ViewBag.ActiveMenu = "TeacherManagement";
            ViewBag.Title = "Teacher Details";

            return View(teacher);
        }

        public async Task<IActionResult> TeacherDelete(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var teacher = await _context.Teachers
                .Include(t => t.User)
                .Include(t => t.Classes) // Include classes to show dependencies
                .FirstOrDefaultAsync(m => m.TeacherId == id);

            if (teacher == null) return NotFound();

            // Check for attendance records marked by this teacher
            var attendanceCount = await _context.Attendances
                .Where(a => a.MarkedByTeacherId == id)
                .CountAsync();

            ViewBag.ActiveMenu = "TeacherManagement";
            ViewBag.Title = "Delete Teacher";
            ViewBag.ClassCount = teacher.Classes?.Count ?? 0;
            ViewBag.AttendanceCount = attendanceCount;

            return View(teacher);
        }

        [HttpPost, ActionName("TeacherDelete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TeacherDeleteConfirmed(string id)
        {
            try
            {
                var teacher = await _context.Teachers
                    .Include(t => t.User)
                    .Include(t => t.Classes) // Include classes to check dependencies
                    .FirstOrDefaultAsync(t => t.TeacherId == id);

                if (teacher != null)
                {
                    var teacherName = teacher.User.FullName;

                    // Check if teacher has assigned classes
                    if (teacher.Classes != null && teacher.Classes.Any())
                    {
                        // Unassign teacher from all classes (set TeacherId to null)
                        foreach (var cls in teacher.Classes)
                        {
                            cls.TeacherId = null;
                        }
                    }

                    // Check if teacher has marked any attendance records
                    var attendanceRecords = await _context.Attendances
                        .Where(a => a.MarkedByTeacherId == id)
                        .ToListAsync();

                    if (attendanceRecords.Any())
                    {
                        // Set MarkedByTeacherId to null for all attendance records
                        foreach (var attendance in attendanceRecords)
                        {
                            attendance.MarkedByTeacherId = null;
                        }
                    }

                    // Now safe to delete the teacher and user
                    _context.Users.Remove(teacher.User); // Cascade delete will remove teacher
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Teacher '{teacherName}' deleted successfully! " +
                        $"{teacher.Classes?.Count ?? 0} class(es) unassigned and {attendanceRecords.Count} attendance record(s) updated.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Teacher not found. It may have already been deleted.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting teacher: {ex.Message}";
                // Log the inner exception for debugging
                if (ex.InnerException != null)
                {
                    TempData["ErrorMessage"] += $" Details: {ex.InnerException.Message}";
                }
            }

            return RedirectToAction(nameof(TeacherIndex));
        }

        // ==================== PARENT MANAGEMENT ====================
        public async Task<IActionResult> ParentIndex()
        {
            ViewBag.ActiveMenu = "ParentManagement";
            ViewBag.Title = "Parent Management";

            var parents = await _context.Parents
                .Include(p => p.User)
                .Include(p => p.Students)
                .ThenInclude(s => s.User)
                .ToListAsync();

            return View(parents);
        }

        public IActionResult ParentCreate()
        {
            ViewBag.ActiveMenu = "ParentManagement";
            ViewBag.Title = "Create Parent";

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ParentCreate(string fullName, string email, 
            string? phoneNumber, string? address, DateTime? dateOfBirth, string? gender,
            IFormFile? profilePicture)
        {
            // Manual validation for required fields
            if (string.IsNullOrWhiteSpace(fullName))
                ModelState.AddModelError("fullName", "Full name is required");

            if (string.IsNullOrWhiteSpace(email))
                ModelState.AddModelError("email", "Email is required");

            // Remove validation for optional profile picture
            ModelState.Remove("profilePicture");

            if (ModelState.IsValid)
            {
                try
                {
                    // Generate random temporary password for new parent
                    var temporaryPassword = _helper.RandomPassword();

                    // Generate IDs using IdGenerator
                    var userId = IdGenerator.GenerateUserId(_context);
                    var parentId = IdGenerator.GenerateParentId(_context);

                    // Handle profile picture upload to S3
                    string? profilePictureUrl = null;
                    if (profilePicture != null && profilePicture.Length > 0)
                    {
                        try
                        {
                            profilePictureUrl = await _s3Service.UploadFileAsync(profilePicture, userId);
                        }
                        catch (Exception uploadEx)
                        {
                            Console.WriteLine($"Warning: Failed to upload profile picture: {uploadEx.Message}");
                            // Don't fail the operation if upload fails - use default
                        }
                    }

                    var user = new User
                    {
                        UserId = userId,
                        FullName = fullName,
                        Email = email,
                        PasswordHash = _helper.HashPassword(temporaryPassword),
                        PhoneNumber = phoneNumber,
                        DateOfBirth = dateOfBirth,
                        Gender = gender,
                        ProfilePicture = profilePictureUrl, // Set profile picture URL or leave null for default icon
                        UserType = "Parent",
                        CreatedDate = DateTime.Now,
                        Status = "active",
                        IsActive = true
                    };
                    _context.Users.Add(user);

                    var parent = new Parent
                    {
                        ParentId = parentId,
                        UserId = userId,
                        Address = address
                    };
                    _context.Parents.Add(parent);

                    await _context.SaveChangesAsync();
                    
                    // Send welcome email to parent
                    try
                    {
                        _helper.SendWelcomeEmail(email, fullName, "Parent", temporaryPassword);
                    }
                    catch (Exception emailEx)
                    {
                        Console.WriteLine($"Warning: Failed to send welcome email to {email}: {emailEx.Message}");
                        // Don't fail the operation if email fails
                    }
                    
                    TempData["SuccessMessage"] = $"Parent '{fullName}' added successfully! A temporary password has been sent to {email}.";
                    return RedirectToAction(nameof(ParentIndex));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error saving parent: {ex.Message}");
                }
            }

            ViewBag.ActiveMenu = "ParentManagement";
            ViewBag.Title = "Create Parent";
            ViewBag.FullName = fullName;
            ViewBag.Email = email;
            ViewBag.PhoneNumber = phoneNumber;
            ViewBag.Address = address;
            ViewBag.DateOfBirth = dateOfBirth?.ToString("yyyy-MM-dd");
            ViewBag.Gender = gender;

            return View();
        }

        public async Task<IActionResult> ParentEdit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var parent = await _context.Parents
                .Include(p => p.User)
                .Include(p => p.Students)
                .ThenInclude(s => s.User) // Include student user data for display
                .FirstOrDefaultAsync(p => p.ParentId == id);

            if (parent == null) return NotFound();

            ViewBag.ActiveMenu = "ParentManagement";
            ViewBag.Title = "Edit Parent";

            return View(parent);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ParentEdit(string parentId, string fullName, string email, 
            string? phoneNumber, string? address, DateTime? dateOfBirth, string? gender, string status,
            IFormFile? profilePicture)
        {
            var parent = await _context.Parents
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.ParentId == parentId);

            if (parent == null)
            {
                TempData["ErrorMessage"] = "Parent not found.";
                return NotFound();
            }

            // Validate required fields
            if (string.IsNullOrWhiteSpace(fullName))
                ModelState.AddModelError("fullName", "Full name is required");

            if (string.IsNullOrWhiteSpace(email))
                ModelState.AddModelError("email", "Email is required");

            if (string.IsNullOrWhiteSpace(status))
                ModelState.AddModelError("status", "Status is required");

            // Remove validation for optional profile picture
            ModelState.Remove("profilePicture");

            if (ModelState.IsValid)
            {
                try
                {
                    // Handle profile picture upload
                    if (profilePicture != null && profilePicture.Length > 0)
                    {
                        try
                        {
                            // Delete old picture if exists
                            if (!string.IsNullOrEmpty(parent.User.ProfilePicture) && 
                                !parent.User.ProfilePicture.StartsWith("/images/"))
                            {
                                await _s3Service.DeleteFileAsync(parent.User.ProfilePicture);
                            }
                            
                            // Upload new picture
                            var profilePictureUrl = await _s3Service.UploadFileAsync(profilePicture, parent.User.UserId);
                            parent.User.ProfilePicture = profilePictureUrl;
                        }
                        catch (Exception uploadEx)
                        {
                            Console.WriteLine($"Warning: Failed to upload profile picture: {uploadEx.Message}");
                        }
                    }

                    // Update User information
                    parent.User.FullName = fullName;
                    parent.User.Email = email;
                    parent.User.PhoneNumber = phoneNumber;
                    parent.User.DateOfBirth = dateOfBirth;
                    parent.User.Gender = gender;
                    parent.User.Status = status;
                    parent.User.IsActive = status == "active";

                    // Update Parent information
                    parent.Address = address;

                    _context.Update(parent);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Parent '{fullName}' updated successfully!";
                    return RedirectToAction(nameof(ParentIndex));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Parents.Any(e => e.ParentId == parentId))
                    {
                        TempData["ErrorMessage"] = "Parent not found. It may have been deleted.";
                        return NotFound();
                    }
                    TempData["ErrorMessage"] = "Unable to save changes. The parent was modified by another user.";
                    throw;
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Error updating parent: {ex.Message}";
                }
            }

            ViewBag.ActiveMenu = "ParentManagement";
            ViewBag.Title = "Edit Parent";
            return View(parent);
        }

        public async Task<IActionResult> ParentDetails(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var parent = await _context.Parents
                .Include(p => p.User)
                .Include(p => p.Students)
                .ThenInclude(s => s.User)
                .FirstOrDefaultAsync(m => m.ParentId == id);

            if (parent == null) return NotFound();

            ViewBag.ActiveMenu = "ParentManagement";
            ViewBag.Title = "Parent Details";

            return View(parent);
        }

        public async Task<IActionResult> ParentDelete(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var parent = await _context.Parents
                .Include(p => p.User)
                .Include(p => p.Students)
                .ThenInclude(s => s.User) // Fix: Include student user data
                .FirstOrDefaultAsync(m => m.ParentId == id);

            if (parent == null) return NotFound();

            ViewBag.ActiveMenu = "ParentManagement";
            ViewBag.Title = "Delete Parent";

            return View(parent);
        }

        [HttpPost, ActionName("ParentDelete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ParentDeleteConfirmed(string id)
        {
            try
            {
                var parent = await _context.Parents
                    .Include(p => p.User)
                    .Include(p => p.Students) // Include students to handle dependencies
                    .FirstOrDefaultAsync(p => p.ParentId == id);

                if (parent != null)
                {
                    var parentName = parent.User.FullName;
                    var studentCount = parent.Students?.Count ?? 0;

                    // Unlink all students from this parent (set ParentId to null)
                    if (parent.Students != null && parent.Students.Any())
                    {
                        foreach (var student in parent.Students)
                        {
                            student.ParentId = null;
                        }
                    }

                    // Now safe to delete the parent and user
                    _context.Users.Remove(parent.User); // Cascade delete will remove parent
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Parent '{parentName}' deleted successfully! {studentCount} student(s) unlinked.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Parent not found. It may have already been deleted.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting parent: {ex.Message}";
                // Log the inner exception for debugging
                if (ex.InnerException != null)
                {
                    TempData["ErrorMessage"] += $" Details: {ex.InnerException.Message}";
                }
            }

            return RedirectToAction(nameof(ParentIndex));
        }

        // ==================== CLASS MANAGEMENT ====================
        public async Task<IActionResult> ClassIndex()
        {
            ViewBag.ActiveMenu = "ClassManagement";
            ViewBag.ActiveSubmenu = "Classes";
            ViewBag.Title = "Class Management";

            var classes = await _context.Classes
                .Include(c => c.Teacher)
                .ThenInclude(t => t.User)
                .Include(c => c.Enrollments)
                .OrderBy(c => c.ClassName)
                .ToListAsync();

            return View(classes);
        }

        public async Task<IActionResult> ClassCreate()
        {
            ViewBag.ActiveMenu = "ClassManagement";
            ViewBag.ActiveSubmenu = "Classes";
            ViewBag.Title = "Create Class";
            ViewBag.Teachers = await _context.Teachers.Include(t => t.User).ToListAsync();
            ViewBag.Subjects = await _context.Subjects.ToListAsync();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClassCreate(string className, string teacherId, string roomNumber,
            string day, string startTime, string endTime, string subjectId, int maxCapacity = 30)
        {
            // Manual validation
            if (string.IsNullOrWhiteSpace(className))
                ModelState.AddModelError("className", "Class name is required");

            if (maxCapacity < 1)
                ModelState.AddModelError("maxCapacity", "Maximum capacity must be at least 1");

            if (ModelState.IsValid)
            {
                var classCount = await _context.Classes.CountAsync();
                var classId = $"C{(classCount + 1):D3}";

                // Parse time strings to TimeSpan
                TimeSpan? parsedStartTime = null;
                TimeSpan? parsedEndTime = null;

                if (!string.IsNullOrEmpty(startTime) && TimeSpan.TryParse(startTime, out var st))
                {
                    parsedStartTime = st;
                }

                if (!string.IsNullOrEmpty(endTime) && TimeSpan.TryParse(endTime, out var et))
                {
                    parsedEndTime = et;
                }

                var @class = new Class
                {
                    ClassId = classId,
                    ClassName = className,
                    TeacherId = string.IsNullOrEmpty(teacherId) ? null : teacherId,
                    SubjectId = string.IsNullOrEmpty(subjectId) ? null : subjectId,
                    RoomNumber = roomNumber,
                    Day = day,
                    StartTime = parsedStartTime,
                    EndTime = parsedEndTime,
                    MaxCapacity = maxCapacity,
                    CurrentCapacity = 0
                };
                _context.Classes.Add(@class);

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Class created successfully!";
                return RedirectToAction(nameof(ClassIndex));
            }

            ViewBag.ActiveMenu = "ClassManagement";
            ViewBag.ActiveSubmenu = "Classes";
            ViewBag.Teachers = await _context.Teachers.Include(t => t.User).ToListAsync();
            ViewBag.Subjects = await _context.Subjects.ToListAsync();
            return View();
        }

        public async Task<IActionResult> ClassEdit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var @class = await _context.Classes
                .Include(c => c.Teacher)
                    .ThenInclude(t => t.User)
                .Include(c => c.Subject)
                .Include(c => c.Enrollments)
                .FirstOrDefaultAsync(c => c.ClassId == id);

            if (@class == null) return NotFound();

            ViewBag.ActiveMenu = "ClassManagement";
            ViewBag.ActiveSubmenu = "Classes";
            ViewBag.Title = "Edit Class";
            ViewBag.Teachers = await _context.Teachers.Include(t => t.User).ToListAsync();
            ViewBag.Subjects = await _context.Subjects.ToListAsync();

            return View(@class);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClassEdit(string classId, string className, string teacherId,
            string subjectId, string roomNumber, string day, string startTime, string endTime, int maxCapacity)
        {
            var @class = await _context.Classes
                .Include(c => c.Enrollments)
                .FirstOrDefaultAsync(c => c.ClassId == classId);

            if (@class == null)
            {
                TempData["ErrorMessage"] = "Class not found.";
                return NotFound();
            }

            // Validate required fields
            if (string.IsNullOrWhiteSpace(className))
                ModelState.AddModelError("className", "Class name is required");

            if (maxCapacity < @class.CurrentCapacity)
                ModelState.AddModelError("maxCapacity", $"Maximum capacity cannot be less than current enrollment ({@class.CurrentCapacity})");

            if (ModelState.IsValid)
            {
                try
                {
                    // Parse time strings to TimeSpan
                    TimeSpan? parsedStartTime = null;
                    TimeSpan? parsedEndTime = null;

                    if (!string.IsNullOrEmpty(startTime) && TimeSpan.TryParse(startTime, out var st))
                    {
                        parsedStartTime = st;
                    }

                    if (!string.IsNullOrEmpty(endTime) && TimeSpan.TryParse(endTime, out var et))
                    {
                        parsedEndTime = et;
                    }

                    // Update class information
                    @class.ClassName = className;
                    @class.TeacherId = string.IsNullOrEmpty(teacherId) ? null : teacherId;
                    @class.SubjectId = string.IsNullOrEmpty(subjectId) ? null : subjectId;
                    @class.RoomNumber = roomNumber;
                    @class.Day = day;
                    @class.StartTime = parsedStartTime;
                    @class.EndTime = parsedEndTime;
                    @class.MaxCapacity = maxCapacity;

                    _context.Update(@class);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Class '{className}' updated successfully!";
                    return RedirectToAction(nameof(ClassIndex));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Classes.Any(e => e.ClassId == classId))
                    {
                        TempData["ErrorMessage"] = "Class not found. It may have been deleted.";
                        return NotFound();
                    }
                    TempData["ErrorMessage"] = "Unable to save changes. The class was modified by another user.";
                    throw;
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Error updating class: {ex.Message}";
                }
            }

            ViewBag.ActiveMenu = "ClassManagement";
            ViewBag.ActiveSubmenu = "Classes";
            ViewBag.Teachers = await _context.Teachers.Include(t => t.User).ToListAsync();
            ViewBag.Subjects = await _context.Subjects.ToListAsync();
            return View(@class);
        }

        public async Task<IActionResult> ClassDetails(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var @class = await _context.Classes
                .Include(c => c.Teacher)
                    .ThenInclude(t => t.User)
                .Include(c => c.Subject)
                .Include(c => c.Enrollments)
                    .ThenInclude(e => e.Student)
                    .ThenInclude(s => s.User)
                .FirstOrDefaultAsync(m => m.ClassId == id);

            if (@class == null) return NotFound();

            ViewBag.ActiveMenu = "ClassManagement";
            ViewBag.ActiveSubmenu = "Classes";
            ViewBag.Title = "Class Details";

            return View(@class);
        }

        public async Task<IActionResult> ClassDelete(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var @class = await _context.Classes
                .Include(c => c.Teacher)
                    .ThenInclude(t => t.User)
                .Include(c => c.Subject)
                .Include(c => c.Enrollments)
                    .ThenInclude(e => e.Student)
                    .ThenInclude(s => s.User)
                .FirstOrDefaultAsync(m => m.ClassId == id);

            if (@class == null) return NotFound();

            ViewBag.ActiveMenu = "ClassManagement";
            ViewBag.ActiveSubmenu = "Classes";
            ViewBag.Title = "Delete Class";

            return View(@class);
        }

        [HttpPost, ActionName("ClassDelete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClassDeleteConfirmed(string id)
        {
            try
            {
                var @class = await _context.Classes
                    .Include(c => c.Enrollments)
                    .Include(c => c.Attendances)
                    .FirstOrDefaultAsync(c => c.ClassId == id);

                if (@class != null)
                {
                    var className = @class.ClassName;
                    var enrollmentCount = @class.Enrollments?.Count ?? 0;
                    var attendanceCount = @class.Attendances?.Count ?? 0;

                    // Cascade delete will remove enrollments and attendances
                    _context.Classes.Remove(@class);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Class '{className}' deleted successfully! " +
                        $"{enrollmentCount} enrollment(s) and {attendanceCount} attendance record(s) removed.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Class not found. It may have already been deleted.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting class: {ex.Message}";
                if (ex.InnerException != null)
                {
                    TempData["ErrorMessage"] += $" Details: {ex.InnerException.Message}";
                }
            }

            return RedirectToAction(nameof(ClassIndex));
        }

        public async Task<IActionResult> ScheduleIndex()
        {
            ViewBag.ActiveMenu = "ClassManagement";
            ViewBag.ActiveSubmenu = "Schedule";
            ViewBag.Title = "Class Schedule";

            var classes = await _context.Classes
                .Include(c => c.Teacher)
                .ThenInclude(t => t.User)
                .Include(c => c.Subject)
                .Include(c => c.Enrollments)
                .Where(c => c.StartTime.HasValue && c.EndTime.HasValue && !string.IsNullOrEmpty(c.Day))
                .OrderBy(c => c.Day)
                .ThenBy(c => c.StartTime)
                .ToListAsync();

            return View(classes);
        }

        // ==================== SUBJECT MANAGEMENT ====================
        public async Task<IActionResult> SubjectIndex()
        {
            ViewBag.ActiveMenu = "ClassManagement";
            ViewBag.ActiveSubmenu = "Subjects";
            ViewBag.Title = "Subject Management";

            var subjects = await _context.Subjects
                .Include(s => s.Classes)
                .OrderBy(s => s.SubjectName)
                .ToListAsync();

            return View(subjects);
        }

        public IActionResult SubjectCreate()
        {
            ViewBag.ActiveMenu = "ClassManagement";
            ViewBag.ActiveSubmenu = "Subjects";
            ViewBag.Title = "Create Subject";

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubjectCreate(string subjectName)
        {
            // Manual validation
            if (string.IsNullOrWhiteSpace(subjectName))
                ModelState.AddModelError("subjectName", "Subject name is required");

            if (ModelState.IsValid)
            {
                try
                {
                    var subjectCount = await _context.Subjects.CountAsync();
                    var subjectId = $"S{(subjectCount + 1):D3}";

                    var subject = new Subject
                    {
                        SubjectId = subjectId,
                        SubjectName = subjectName
                    };
                    _context.Subjects.Add(subject);

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Subject '{subjectName}' created successfully!";
                    return RedirectToAction(nameof(SubjectIndex));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error saving subject: {ex.Message}");
                }
            }

            ViewBag.ActiveMenu = "ClassManagement";
            ViewBag.ActiveSubmenu = "Subjects";
            ViewBag.Title = "Create Subject";
            return View();
        }

        public async Task<IActionResult> SubjectEdit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var subject = await _context.Subjects
                .Include(s => s.Classes)
                .FirstOrDefaultAsync(s => s.SubjectId == id);

            if (subject == null) return NotFound();

            ViewBag.ActiveMenu = "ClassManagement";
            ViewBag.ActiveSubmenu = "Subjects";
            ViewBag.Title = "Edit Subject";

            return View(subject);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubjectEdit(string subjectId, string subjectName)
        {
            var subject = await _context.Subjects
                .Include(s => s.Classes)
                .FirstOrDefaultAsync(s => s.SubjectId == subjectId);

            if (subject == null)
            {
                TempData["ErrorMessage"] = "Subject not found.";
                return NotFound();
            }

            // Validate required fields
            if (string.IsNullOrWhiteSpace(subjectName))
                ModelState.AddModelError("subjectName", "Subject name is required");

            if (ModelState.IsValid)
            {
                try
                {
                    subject.SubjectName = subjectName;

                    _context.Update(subject);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Subject '{subjectName}' updated successfully!";
                    return RedirectToAction(nameof(SubjectIndex));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Subjects.Any(e => e.SubjectId == subjectId))
                    {
                        TempData["ErrorMessage"] = "Subject not found. It may have been deleted.";
                        return NotFound();
                    }
                    TempData["ErrorMessage"] = "Unable to save changes. The subject was modified by another user.";
                    throw;
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Error updating subject: {ex.Message}";
                }
            }

            ViewBag.ActiveMenu = "ClassManagement";
            ViewBag.ActiveSubmenu = "Subjects";
            ViewBag.Title = "Edit Subject";
            return View(subject);
        }

        public async Task<IActionResult> SubjectDetails(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var subject = await _context.Subjects
                .Include(s => s.Classes)
                    .ThenInclude(c => c.Teacher)
                    .ThenInclude(t => t.User)
                .Include(s => s.Classes)
                    .ThenInclude(c => c.Enrollments)
                .FirstOrDefaultAsync(m => m.SubjectId == id);

            if (subject == null) return NotFound();

            ViewBag.TotalClasses = subject.Classes?.Count ?? 0;
            ViewBag.TotalStudents = subject.Classes?.Sum(c => c.CurrentCapacity) ?? 0;
            ViewBag.ActiveMenu = "ClassManagement";
            ViewBag.ActiveSubmenu = "Subjects";
            ViewBag.Title = "Subject Details";

            return View(subject);
        }

        public async Task<IActionResult> SubjectDelete(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var subject = await _context.Subjects
                .Include(s => s.Classes)
                    .ThenInclude(c => c.Teacher)
                    .ThenInclude(t => t.User)
                .FirstOrDefaultAsync(m => m.SubjectId == id);

            if (subject == null) return NotFound();

            ViewBag.ActiveMenu = "ClassManagement";
            ViewBag.ActiveSubmenu = "Subjects";
            ViewBag.Title = "Delete Subject";

            return View(subject);
        }

        [HttpPost, ActionName("SubjectDelete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubjectDeleteConfirmed(string id)
        {
            try
            {
                var subject = await _context.Subjects
                    .Include(s => s.Classes)
                    .FirstOrDefaultAsync(s => s.SubjectId == id);

                if (subject != null)
                {
                    var subjectName = subject.SubjectName;
                    var classCount = subject.Classes?.Count ?? 0;

                    // Unassign subject from all classes (set SubjectId to null)
                    if (subject.Classes != null && subject.Classes.Any())
                    {
                        foreach (var cls in subject.Classes)
                        {
                            cls.SubjectId = null;
                        }
                    }

                    // Now safe to delete the subject
                    _context.Subjects.Remove(subject);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Subject '{subjectName}' deleted successfully! {classCount} class(es) unassigned.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Subject not found. It may have already been deleted.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting subject: {ex.Message}";
                if (ex.InnerException != null)
                {
                    TempData["ErrorMessage"] += $" Details: {ex.InnerException.Message}";
                }
            }

            return RedirectToAction(nameof(SubjectIndex));
        }

        // ==================== ATTENDANCE MANAGEMENT ====================        // Take Attendance with PIN Code
        public async Task<IActionResult> AttendanceTake(DateTime? selectedDate)
        {
            ViewBag.ActiveMenu = "AttendanceManagement";
            ViewBag.ActiveSubmenu = "Take";
            ViewBag.Title = "Take Attendance";

            // Use selected date or default to today
            var targetDate = selectedDate ?? DateTime.Today;
            ViewBag.SelectedDate = targetDate;
            ViewBag.SelectedDay = targetDate.DayOfWeek.ToString();

            // Load all classes
            ViewBag.Classes = await _context.Classes
                .Include(c => c.Teacher)
                .ThenInclude(t => t.User)
                .Include(c => c.Enrollments)
                .OrderBy(c => c.ClassName)
                .ToListAsync();

            // Load ONLY sessions created on the selected date
            // CreatedDate must match the selected date exactly
            ViewBag.Sessions = await _context.AttendanceSessions
                .Include(s => s.Class)
                .Where(s => s.IsActive && s.CreatedDate.Date == targetDate.Date)
                .ToListAsync();

            return View();
        }

        // Attendance Class Detail - for generating PIN
        public async Task<IActionResult> AttendanceClassDetail(string id, DateTime? selectedDate)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var classEntity = await _context.Classes
                .Include(c => c.Teacher)
                    .ThenInclude(t => t.User)
                .Include(c => c.Subject)
                .Include(c => c.Enrollments)
                    .ThenInclude(e => e.Student)
                    .ThenInclude(s => s.User)
                .FirstOrDefaultAsync(c => c.ClassId == id);

            if (classEntity == null) return NotFound();

            ViewBag.ActiveMenu = "AttendanceManagement";
            ViewBag.ActiveSubmenu = "Take";

            // Use selected date or default to today
            var targetDate = selectedDate ?? DateTime.Today;
            ViewBag.SelectedDate = targetDate;

            // Load ONLY the session created on the selected date for this class
            // This ensures we show the correct PIN for the selected date
            ViewBag.Sessions = await _context.AttendanceSessions
                .Where(s => s.IsActive && 
                           s.ClassId == id && 
                           s.CreatedDate.Date == targetDate.Date)
                .ToListAsync();

            // Load ALL sessions for this class (to show if there's a PIN from a different date)
            ViewBag.AllSessions = await _context.AttendanceSessions
                .Where(s => s.IsActive && s.ClassId == id)
                .OrderByDescending(s => s.CreatedDate)
                .ToListAsync();

            // Load attendance records for this class on the selected date
            ViewBag.TodayAttendances = await _context.Attendances
                .Where(a => a.ClassId == id && a.Date.Date == targetDate.Date)
                .ToListAsync();

            return View(classEntity);
        }

        [HttpPost]
        public async Task<IActionResult> SaveManualAttendance([FromBody] ManualAttendanceRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.ClassId) || request.Attendances == null || !request.Attendances.Any())
                {
                    return Json(new { success = false, message = "Invalid request data" });
                }

                var selectedDate = DateTime.Parse(request.Date);
                int markedCount = 0;
                var errors = new List<string>();

                // Get the current maximum attendance count ONCE outside the loop
                var currentAttendanceCount = await _context.Attendances.CountAsync();

                foreach (var att in request.Attendances)
                {
                    // Check if attendance already exists for this student on this date
                    var existing = await _context.Attendances
                        .FirstOrDefaultAsync(a => a.StudentId == att.StudentId &&
                                                  a.ClassId == request.ClassId &&
                                                  a.Date.Date == selectedDate.Date);

                    if (existing != null)
                    {
                        // Update existing attendance
                        if (existing.Status != att.Status)
                        {
                            existing.Status = att.Status;
                            existing.TakenOn = DateTime.Now;
                            _context.Update(existing);
                            markedCount++;
                        }
                    }
                    else
                    {
                        // Create new attendance record
                        currentAttendanceCount++;
                        var attId = $"ATT{currentAttendanceCount:D5}";

                        var attendance = new Attendance
                        {
                            AttendanceId = attId,
                            StudentId = att.StudentId,
                            ClassId = request.ClassId,
                            Date = selectedDate,
                            TakenOn = DateTime.Now,
                            Status = att.Status,
                            MarkedByTeacherId = null // Could be set to current admin user
                        };
                        _context.Attendances.Add(attendance);
                        markedCount++;
                    }
                }

                // Save all changes at once
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    marked = markedCount,
                    errors = errors.Count > 0 ? errors : null,
                    message = $"Successfully saved attendance for {markedCount} student(s)"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SaveSingleAttendance([FromBody] SingleAttendanceRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.ClassId) || string.IsNullOrEmpty(request.StudentId))
                {
                    return Json(new { success = false, message = "Invalid request data" });
                }

                var selectedDate = DateTime.Parse(request.Date);

                // Get the class to validate schedule
                var classEntity = await _context.Classes
                    .FirstOrDefaultAsync(c => c.ClassId == request.ClassId);

                if (classEntity == null)
                {
                    return Json(new { success = false, message = "Class not found" });
                }

                // Validate that admin can only mark attendance after class has started
                if (classEntity.StartTime.HasValue)
                {
                    // Check if the selected date is before today
                    if (selectedDate.Date < DateTime.Today)
                    {
                        // Past date - admin can mark attendance (already passed)
                    }
                    else if (selectedDate.Date == DateTime.Today)
                    {
                        // Today - check if class has started
                        var currentTime = DateTime.Now.TimeOfDay;
                        if (currentTime < classEntity.StartTime.Value)
                        {
                            return Json(new
                            {
                                success = false,
                                message = $"Attendance can only be marked after class starts at {classEntity.StartTime.Value:hh\\:mm}. Current time: {DateTime.Now:hh\\:mm tt}"
                            });
                        }
                    }
                    else
                    {
                        // Future date - cannot mark attendance
                        return Json(new
                        {
                            success = false,
                            message = "Attendance cannot be marked for future dates"
                        });
                    }
                }

                // Validate that the selected date matches the class day
                var selectedDayOfWeek = selectedDate.DayOfWeek.ToString();
                if (!string.IsNullOrEmpty(classEntity.Day) && !classEntity.Day.Equals(selectedDayOfWeek, StringComparison.OrdinalIgnoreCase))
                {
                    return Json(new
                    {
                        success = false,
                        message = $"This class is scheduled for {classEntity.Day}, not {selectedDayOfWeek}"
                    });
                }

                // Check if student is enrolled in this class
                var isEnrolled = await _context.Enrollments
                    .AnyAsync(e => e.StudentId == request.StudentId && e.ClassId == request.ClassId);

                if (!isEnrolled)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Student is not enrolled in this class"
                    });
                }

                // Check if attendance already exists for this student on this date
                var existing = await _context.Attendances
                    .FirstOrDefaultAsync(a => a.StudentId == request.StudentId &&
                                              a.ClassId == request.ClassId &&
                                              a.Date.Date == selectedDate.Date);

                if (existing != null)
                {
                    // Update existing attendance
                    existing.Status = request.Status;
                    existing.TakenOn = DateTime.Now;
                    _context.Update(existing);
                }
                else
                {
                    // Create new attendance record
                    var currentAttendanceCount = await _context.Attendances.CountAsync();
                    var attId = $"ATT{(currentAttendanceCount + 1):D5}";

                    var attendance = new Attendance
                    {
                        AttendanceId = attId,
                        StudentId = request.StudentId,
                        ClassId = request.ClassId,
                        Date = selectedDate,
                        TakenOn = DateTime.Now,
                        Status = request.Status,
                        MarkedByTeacherId = null // Could be set to current admin user
                    };

                    _context.Attendances.Add(attendance);
                }

                // Save changes
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"Attendance saved: {request.Status}"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }


        // View Attendance Records with filters
        public async Task<IActionResult> AttendanceRecords(string? studentId, string? classId, DateTime? startDate, DateTime? endDate)
        {
            ViewBag.ActiveMenu = "AttendanceManagement";
            ViewBag.ActiveSubmenu = "Records";
            ViewBag.Title = "Attendance Records";

            // Build query
            var query = _context.Attendances
                .Include(a => a.Student)
                    .ThenInclude(s => s.User)
                .Include(a => a.Class)
                .Include(a => a.MarkedByTeacher)
                    .ThenInclude(t => t.User)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(studentId))
            {
                query = query.Where(a => a.StudentId == studentId);
                ViewBag.SelectedStudentId = studentId;
            }

            if (!string.IsNullOrEmpty(classId))
            {
                query = query.Where(a => a.ClassId == classId);
                ViewBag.SelectedClassId = classId;
            }

            if (startDate.HasValue)
            {
                query = query.Where(a => a.Date >= startDate.Value);
                ViewBag.StartDate = startDate.Value.ToString("yyyy-MM-dd");
            }

            if (endDate.HasValue)
            {
                query = query.Where(a => a.Date <= endDate.Value);
                ViewBag.EndDate = endDate.Value.ToString("yyyy-MM-dd");
            }

            // Get filtered records
            var records = await query
                .OrderByDescending(a => a.Date)
                .ThenByDescending(a => a.TakenOn)
                .ToListAsync();

            // Calculate statistics
            var totalRecords = records.Count;
            var presentCount = records.Count(a => a.Status == "Present");
            var absentCount = records.Count(a => a.Status == "Absent");
            var lateCount = records.Count(a => a.Status == "Late");
            var attendanceRate = totalRecords > 0 ? Math.Round((decimal)presentCount / totalRecords * 100, 1) : 0;

            ViewBag.TotalRecords = totalRecords;
            ViewBag.PresentCount = presentCount;
            ViewBag.AbsentCount = absentCount;
            ViewBag.LateCount = lateCount;
            ViewBag.AttendanceRate = attendanceRate;
            ViewBag.Students = await _context.Students.Include(s => s.User).ToListAsync();
            ViewBag.Classes = await _context.Classes.ToListAsync();

            return View(records);
        }

        // Reports & Analytics
        public async Task<IActionResult> Reports()
        {
            ViewBag.ActiveMenu = "Reports";
            ViewBag.Title = "Reports & Analytics";

            // Get total counts
            var totalStudents = await _context.Students.CountAsync();
            var totalTeachers = await _context.Teachers.CountAsync();
            var totalClasses = await _context.Classes.CountAsync();
            var totalAttendanceRecords = await _context.Attendances.CountAsync();

            // Get this month's data
            var startOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

            var thisMonthAttendances = await _context.Attendances
                .Where(a => a.Date >= startOfMonth && a.Date <= endOfMonth)
                .ToListAsync();

            var thisMonthPresent = thisMonthAttendances.Count(a => a.Status == "Present");
            var thisMonthAbsent = thisMonthAttendances.Count(a => a.Status == "Absent");
            var thisMonthLate = thisMonthAttendances.Count(a => a.Status == "Late");
            var thisMonthTotal = thisMonthAttendances.Count;
            var thisMonthRate = thisMonthTotal > 0 ? Math.Round((decimal)thisMonthPresent / thisMonthTotal * 100, 1) : 0;

            // Get weekly attendance data for trend chart (last 4 weeks)
            var weeklyData = new List<object>();
            for (int weekOffset = 3; weekOffset >= 0; weekOffset--)
            {
                var weekStart = startOfMonth.AddDays(weekOffset * 7);
                var weekEnd = weekStart.AddDays(7);
                
                var weekAttendances = await _context.Attendances
                    .Where(a => a.Date >= weekStart && a.Date < weekEnd)
                    .ToListAsync();
                
                weeklyData.Add(new
                {
                    present = weekAttendances.Count(a => a.Status == "Present"),
                    absent = weekAttendances.Count(a => a.Status == "Absent"),
                    late = weekAttendances.Count(a => a.Status == "Late")
                });
            }
            ViewBag.WeeklyData = weeklyData;

            // Get top performing students (by attendance rate)
            var studentAttendanceStats = await _context.Students
                .Include(s => s.User)
                .Include(s => s.Attendances)
                .Where(s => s.Attendances.Any())
                .Select(s => new
                {
                    StudentId = s.StudentId,
                    FullName = s.User.FullName,
                    TotalAttendance = s.Attendances.Count,
                    PresentCount = s.Attendances.Count(a => a.Status == "Present"),
                    AttendanceRate = s.Attendances.Count > 0 
                        ? Math.Round((decimal)s.Attendances.Count(a => a.Status == "Present") / s.Attendances.Count * 100, 1) 
                        : 0
                })
                .OrderByDescending(s => s.AttendanceRate)
                .ThenByDescending(s => s.TotalAttendance)
                .Take(5)
                .ToListAsync();
            
            ViewBag.TopStudents = studentAttendanceStats;

            // Get class performance data (enrollment and capacity)
            var classPerformance = await _context.Classes
                .Include(c => c.Enrollments)
                .Where(c => c.Enrollments.Any())
                .Select(c => new
                {
                    ClassId = c.ClassId,
                    ClassName = c.ClassName,
                    CurrentCapacity = c.CurrentCapacity,
                    MaxCapacity = c.MaxCapacity,
                    FillRate = c.MaxCapacity > 0 
                        ? Math.Round((decimal)c.CurrentCapacity / c.MaxCapacity * 100, 1) 
                        : 0,
                    Status = c.CurrentCapacity >= c.MaxCapacity ? "Full" :
                            c.CurrentCapacity >= (c.MaxCapacity * 0.9) ? "Almost Full" : "Active"
                })
                .OrderByDescending(c => c.FillRate)
                .Take(5)
                .ToListAsync();
            
            ViewBag.ClassPerformance = classPerformance;

            // Set ViewBag data
            ViewBag.TotalStudents = totalStudents;
            ViewBag.TotalTeachers = totalTeachers;
            ViewBag.TotalClasses = totalClasses;
            ViewBag.TotalAttendanceRecords = totalAttendanceRecords;
            ViewBag.ThisMonthPresent = thisMonthPresent;
            ViewBag.ThisMonthAbsent = thisMonthAbsent;
            ViewBag.ThisMonthLate = thisMonthLate;
            ViewBag.ThisMonthRate = thisMonthRate;

            return View();
        }

        // ==================== LEAVE MANAGEMENT ====================

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
                    documentUrls = System.Text.Json.JsonSerializer.Deserialize<List<string>>(leave.DocumentPaths) ?? new List<string>();
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
                leave.Remarks = remarks;

                // Get the student record
                var student = await _context.Students
                    .Include(s => s.Enrollments)
                        .ThenInclude(e => e.Class)
                    .FirstOrDefaultAsync(s => s.UserId == leave.UserId);

                if (student != null && student.Enrollments.Any())
                {
                    // Get current attendance count for ID generation
                    var currentAttendanceCount = await _context.Attendances.CountAsync();
                    int attendanceRecordsCreated = 0;

                    // For each day in the leave period
                    for (var date = leave.StartDate.Date; date <= leave.EndDate.Date; date = date.AddDays(1))
                    {
                        // Get the day of week for this date
                        var dayOfWeek = date.DayOfWeek.ToString();

                        // For each class the student is enrolled in
                        foreach (var enrollment in student.Enrollments)
                        {
                            // Check if the class is scheduled on this day
                            if (!string.IsNullOrEmpty(enrollment.Class.Day) && 
                                enrollment.Class.Day.Equals(dayOfWeek, StringComparison.OrdinalIgnoreCase))
                            {
                                // Check if attendance already exists for this date and class
                                var existingAttendance = await _context.Attendances
                                    .FirstOrDefaultAsync(a => a.StudentId == student.StudentId && 
                                                            a.ClassId == enrollment.ClassId && 
                                                            a.Date.Date == date.Date);

                                if (existingAttendance == null)
                                {
                                    // Create new attendance record with "Leave" status
                                    currentAttendanceCount++;
                                    var attendance = new Attendance
                                    {
                                        AttendanceId = $"ATT{currentAttendanceCount:D5}",
                                        StudentId = student.StudentId,
                                        ClassId = enrollment.ClassId,
                                        Date = date,
                                        TakenOn = DateTime.Now,
                                        Status = "Leave",
                                        MarkedByTeacherId = null
                                    };
                                    _context.Attendances.Add(attendance);
                                    attendanceRecordsCreated++;
                                }
                                else if (existingAttendance.Status != "Leave")
                                {
                                    // Update existing records to "Leave" (any status except Leave itself)
                                    existingAttendance.Status = "Leave";
                                    existingAttendance.TakenOn = DateTime.Now;
                                    _context.Update(existingAttendance);
                                    attendanceRecordsCreated++;
                                }
                            }
                        }
                    }

                    Console.WriteLine($"[ApproveLeave] Created/Updated {attendanceRecordsCreated} attendance records with 'Leave' status");
                }

                // Create notification for student
                var notificationCount = await _context.Notifications.CountAsync();
                var notification = new Notification
                {
                    NotificationId = $"N{(notificationCount + 1):D5}",
                    UserId = leave.UserId,
                    Description = $"Your leave application from {leave.StartDate:dd MMM yyyy} to {leave.EndDate:dd MMM yyyy} has been approved." + 
                                  (string.IsNullOrEmpty(remarks) ? "" : $" Remarks: {remarks}"),
                    Status = "unread",
                    CreatedDate = DateTime.Now
                };
                _context.Notifications.Add(notification);

                await _context.SaveChangesAsync();

                // Send email to student
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

                                    <div style='background-color: #d1ecf1; padding: 15px; border-radius: 5px; border-left: 4px solid #0c5460;'>
                                      <p style='margin: 0; color: #0c5460;'>
                                        <strong>Note:</strong> Your attendance for the approved leave period has been automatically marked as Leave 
                                        and will be counted as present for attendance rate calculations.
                                      </p>
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

                TempData["SuccessMessage"] = $"Leave application for {leave.User.FullName} has been approved. Attendance records have been automatically marked as 'Leave'.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error approving leave: {ex.Message}";
                Console.WriteLine($"Error in ApproveLeave: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
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
                    NotificationId = $"N{(notificationCount + 1):D5}",
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
                                      <p style='margin: 5px 0;'><strong>Reason:</strong> {leave.Reason}</p>
                                      {(string.IsNullOrEmpty(remarks) ? "" : $"<p style='margin: 5px 0;'><strong>Admin Remarks:</strong> {remarks}</p>")}
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
                            var documentUrls = System.Text.Json.JsonSerializer.Deserialize<List<string>>(leave.DocumentPaths) ?? new List<string>();
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

        // ==================== SETTINGS ====================

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Settings()
        {
            ViewBag.ActiveMenu = "Settings";
            ViewBag.ActiveSubmenu = "Settings";
            ViewBag.Title = "Admin Settings";

            // Get current admin user
            var userEmail = User.Identity.Name;
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == userEmail);

            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Dashboard");
            }

            return View(user);
        }

        // ==================== PROFILE PICTURE UPLOAD ENDPOINTS ====================

        [HttpPost]
        public async Task<IActionResult> UploadProfilePicture(IFormFile file, string userId)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return Json(new { success = false, message = "No file uploaded" });
                }

                // Validate file type
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                {
                    return Json(new { success = false, message = "Invalid file type. Only JPG, PNG, GIF, and WEBP are allowed." });
                }

                // Validate file size (5 MB)
                if (file.Length > 5 * 1024 * 1024)
                {
                    return Json(new { success = false, message = "File size must not exceed 5 MB" });
                }

                // Find user
                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found" });
                }

                // Delete old profile picture if exists
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

                // Upload to S3
                var s3Url = await _s3Service.UploadFileAsync(file, userId);

                // Update user profile picture
                user.ProfilePicture = s3Url;
                _context.Update(user);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Profile picture uploaded successfully!", url = s3Url });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uploading profile picture: {ex.Message}");
                return Json(new { success = false, message = $"Upload failed: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteProfilePicture([FromBody] DeleteProfilePictureRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request?.UserId))
                {
                    return Json(new { success = false, message = "User ID is required" });
                }

                // Find user
                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == request.UserId);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found" });
                }

                // Delete from S3 if exists
                if (!string.IsNullOrEmpty(user.ProfilePicture) && !user.ProfilePicture.StartsWith("/images/"))
                {
                    try
                    {
                        await _s3Service.DeleteFileAsync(user.ProfilePicture);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Failed to delete from S3: {ex.Message}");
                    }
                }

                // Update user profile picture to default
                user.ProfilePicture = "/images/default-avatar.png";
                _context.Update(user);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Profile picture removed successfully!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting profile picture: {ex.Message}");
                return Json(new { success = false, message = $"Delete failed: {ex.Message}" });
            }
        }

        public class DeleteProfilePictureRequest
        {
            public string UserId { get; set; }
        }

        // ==================== NOTIFICATIONS ====================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Notifications()
        {
            ViewBag.ActiveMenu = "Notifications";
            ViewBag.Title = "Notifications";

            // Get current admin user
            var userEmail = User.Identity.Name;
            var admin = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == userEmail && u.UserType == "Admin");

            if (admin == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Get notifications for this admin user
            var notifications = await _context.Notifications
                .Include(n => n.User)
                .Where(n => n.UserId == admin.UserId)
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
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(string notificationId)
        {
            try
            {
                var userEmail = User.Identity.Name;
                var admin = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == userEmail && u.UserType == "Admin");

                if (admin == null)
                {
                    return Json(new { success = false, message = "Unauthorized" });
                }

                var notification = await _context.Notifications
                    .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == admin.UserId);

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
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAsRead()
        {
            try
            {
                var userEmail = User.Identity.Name;
                var admin = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == userEmail && u.UserType == "Admin");

                if (admin == null)
                {
                    return Json(new { success = false, message = "Unauthorized" });
                }

                var notifications = await _context.Notifications
                    .Where(n => n.UserId == admin.UserId && n.Status == "unread")
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

        // Get unread notification count (for layout badge)
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetUnreadNotificationCount()
        {
            try
            {
                var userEmail = User.Identity.Name;
                var admin = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == userEmail && u.UserType == "Admin");

                if (admin == null)
                {
                    return Json(new { success = false, count = 0 });
                }

                var unreadCount = await _context.Notifications
                    .CountAsync(n => n.UserId == admin.UserId && n.Status == "unread");

                return Json(new { success = true, count = unreadCount });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, count = 0, message = ex.Message });
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(string userId, string fullName, string email, string? phoneNumber)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Settings");
            }

            // Validate required fields
            if (string.IsNullOrWhiteSpace(fullName))
                ModelState.AddModelError("fullName", "Full name is required");

            if (string.IsNullOrWhiteSpace(email))
                ModelState.AddModelError("email", "Email is required");

            if (ModelState.IsValid)
            {
                try
                {
                    // Update user information
                    user.FullName = fullName;
                    user.Email = email;
                    user.PhoneNumber = phoneNumber;

                    _context.Update(user);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Settings updated successfully!";
                    return RedirectToAction("Settings");
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Error updating settings: {ex.Message}";
                }
            }

            ViewBag.ActiveMenu = "Settings";
            ViewBag.ActiveSubmenu = "Settings";
            ViewBag.Title = "Admin Settings";
            return View(user);
        }

        [Authorize(Roles = "Admin")]
        public IActionResult ChangePassword()
        {
            ViewBag.ActiveMenu = "Settings";
            ViewBag.ActiveSubmenu = "ChangePassword";
            ViewBag.Title = "Change Password";

            return View();
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(currentPassword))
                ModelState.AddModelError("currentPassword", "Current password is required");

            if (string.IsNullOrWhiteSpace(newPassword))
                ModelState.AddModelError("newPassword", "New password is required");

            if (string.IsNullOrWhiteSpace(confirmPassword))
                ModelState.AddModelError("confirmPassword", "Confirm password is required");

            if (newPassword != confirmPassword)
                ModelState.AddModelError("confirmPassword", "Passwords do not match");

            if (newPassword != null && newPassword.Length < 6)
                ModelState.AddModelError("newPassword", "Password must be at least 6 characters long");

            if (ModelState.IsValid)
            {
                try
                {
                    // Get current admin user
                    var userEmail = User.Identity.Name;
                    var user = await _context.Users
                        .FirstOrDefaultAsync(u => u.Email == userEmail);

                    if (user == null)
                    {
                        TempData["ErrorMessage"] = "User not found.";
                        return RedirectToAction("ChangePassword");
                    }

                    // Verify current password
                    if (!_helper.VerifyPassword(currentPassword, user.PasswordHash))
                    {
                        ModelState.AddModelError("currentPassword", "Current password is incorrect");
                        ViewBag.ActiveMenu = "Settings";
                        ViewBag.ActiveSubmenu = "ChangePassword";
                        ViewBag.Title = "Change Password";
                        return View();
                    }

                    // Update password
                    user.PasswordHash = _helper.HashPassword(newPassword);
                    _context.Update(user);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Password changed successfully!";
                    return RedirectToAction("ChangePassword");
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Error changing password: {ex.Message}";
                }
            }

            ViewBag.ActiveMenu = "Settings";
            ViewBag.ActiveSubmenu = "ChangePassword";
            ViewBag.Title = "Change Password";
            return View();
        }
        // ==================== REQUEST MODELS ====================

        // Request models for bulk operations
        public class BulkAttendanceRequest
        {
            public string ClassId { get; set; }
            public string PinCode { get; set; }
            public string Date { get; set; }
            public List<AttendanceItem> Attendances { get; set; }
        }

        public class AttendanceItem
        {
            public string StudentId { get; set; }
            public string Status { get; set; }
        }

        // Request model for manual attendance saving
        public class ManualAttendanceRequest
        {
            public string ClassId { get; set; }
            public string PinCode { get; set; }
            public string Date { get; set; }
            public List<ManualAttendanceItem> Attendances { get; set; }
        }

        public class ManualAttendanceItem
        {
            public string StudentId { get; set; }
            public string Status { get; set; }
        }

        // Request model for single attendance saving
        public class SingleAttendanceRequest
        {
            public string ClassId { get; set; }
            public string StudentId { get; set; }
            public string Date { get; set; }
            public string Status { get; set; }
        }
    }
}
    



