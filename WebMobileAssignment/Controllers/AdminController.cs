using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebMobileAssignment.Models;
using WebMobileAssignment.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace WebMobileAssignment.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly DB _context;
        private readonly Helper _helper;
        private readonly S3Service _s3Service;
        private readonly LocalizationService _localization;

        public AdminController(DB context, Helper helper, S3Service s3Service, LocalizationService localization)
        {
            _context = context;
          _helper = helper;
          _s3Service = s3Service;
          _localization = localization;
        }

        // ==================== DASHBOARD ====================
        public async Task<IActionResult> Dashboard()
        {
            ViewBag.ActiveMenu = "Dashboard";
            ViewBag.Title = _localization["Dashboard"];
            ViewBag.Localization = _localization;

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

            var leaveToday = await _context.Attendances
                .Where(a => a.Date.Date == DateTime.Today && a.Status == "Leave")
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
            ViewBag.LeaveToday = leaveToday;

            return View(recentAttendance);
        }

        // ==================== STUDENT MANAGEMENT ====================
        public async Task<IActionResult> StudentIndex()
        {
            ViewBag.ActiveMenu = "StudentManagement";
            ViewBag.Title = _localization["StudentManagement"];
            ViewBag.Localization = _localization;

            var students = await _context.Students
                .Include(s => s.User)
                .Include(s => s.Parent)
                    .ThenInclude(p => p.User)
                .Include(s => s.Enrollments)
                    .ThenInclude(e => e.Class)
                .OrderBy(s => s.StudentId)
                .ToListAsync();

            // Filter out unenrolled classes for all students
            foreach (var student in students)
            {
                if (student.Enrollments != null)
                {
                    student.Enrollments = student.Enrollments
                        .Where(e => e.UnenrolledDate == null)
                        .ToList();
                }
            }

            return View(students);
        }

        public async Task<IActionResult> AddStudent()
        {
            ViewBag.ActiveMenu = "StudentManagement";
            ViewBag.Title = _localization["AddNewStudent"];
            ViewBag.Localization = _localization;
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
            // Validate parent requirement - must have either existing parent or new parent details
            bool hasExistingParent = !string.IsNullOrWhiteSpace(parentId);
            bool creatingNewParent = !string.IsNullOrWhiteSpace(newParentEmail);
            
            if (!hasExistingParent && !creatingNewParent)
            {
                ModelState.AddModelError("parentId", "Parent is required. Please select an existing parent or provide new parent details.");
                ModelState.AddModelError("newParentEmail", "Parent is required. Please select an existing parent or provide new parent details.");
            }
            
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
                parentId = null!;
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

            // Validate that EITHER parentId is provided OR new parent details are provided
            if (string.IsNullOrEmpty(parentId) && string.IsNullOrWhiteSpace(newParentEmail))
            {
                ModelState.AddModelError("parentId", "Please select an existing parent or create a new parent");
                ModelState.AddModelError("newParentEmail", "Please select an existing parent or enter new parent email");
            }

            // Validate new parent fields if creating a new parent
            if (string.IsNullOrEmpty(parentId) && !string.IsNullOrWhiteSpace(newParentEmail))
            {
                if (string.IsNullOrWhiteSpace(newParentFullName))
                    ModelState.AddModelError("newParentFullName", "Parent full name is required when creating a new parent");
                    
                if (string.IsNullOrWhiteSpace(newParentPhone))
                    ModelState.AddModelError("newParentPhone", "Parent phone number is required when creating a new parent");
                    
                if (string.IsNullOrWhiteSpace(newParentAddress))
                    ModelState.AddModelError("newParentAddress", "Parent address is required when creating a new parent");
                    
                if (!newParentDateOfBirth.HasValue)
                    ModelState.AddModelError("newParentDateOfBirth", "Parent date of birth is required when creating a new parent");
                    
                if (string.IsNullOrWhiteSpace(newParentGender))
                    ModelState.AddModelError("newParentGender", "Parent gender is required when creating a new parent");
            }
            else if (!string.IsNullOrEmpty(parentId))
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
                    bool isNewParentCreated = false;
                    string? newlyCreatedParentUserId = null;

                    // If need to create new parent
                    if (string.IsNullOrEmpty(parentId) && !string.IsNullOrWhiteSpace(newParentEmail))
                    {
                        isNewParentCreated = true;
                        // create parent user and parent with all fields using IdGenerator
                        var parentUserId = IdGenerator.GenerateUserId(_context);
                        newlyCreatedParentUserId = parentUserId;
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
                            PhoneNumber = newParentPhone,
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
                        
                        // Notify admins about new parent registration
                        if (isNewParentCreated)
                        {
                            var adminUsersForParent = await _context.Users.Where(u => u.UserType == "Admin").ToListAsync();
                            foreach (var admin in adminUsersForParent)
                            {
                                var parentNotificationId = IdGenerator.GenerateNotificationId(_context);
                                var parentNotification = new Notification
                                {
                                    NotificationId = parentNotificationId,
                                    UserId = admin.UserId,
                                    Type = "Parent Registration",
                                    Description = $"New parent registered: {newParentFullName} ({newParentEmail})",
                                    RelatedEntityId = parentUserId,
                                    Status = "unread",
                                    CreatedDate = DateTime.Now
                                };
                                _context.Notifications.Add(parentNotification);
                            }
                            await _context.SaveChangesAsync();
                        }
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
                        DateOfBirth = dateOfBirth!.Value,
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
                            // Generate enrollment ID
                            var enrollmentId = IdGenerator.GenerateEnrollmentId(_context);
                            
                            // Create enrollment
                            var enrollment = new Enrollment
                            {
                                EnrollmentId = enrollmentId,
                                StudentId = studentId,
                                ClassId = classId,
                                EnrolledDate = DateTime.Now,
                                UnenrolledDate = null // Student is enrolled
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
                        
                        // Check capacity alert for admin for each enrolled class
                        if (classIds != null && classIds.Any())
                        {
                            foreach (var classId in classIds)
                            {
                                await CheckAndNotifyClassCapacity(classId);
                            }
                        }
                        
                        // Notify parent about child enrollment in classes
                        if (classIds != null && classIds.Any() && !string.IsNullOrEmpty(parentId))
                        {
                            var parent = await _context.Parents
                                .Include(p => p.User)
                                .FirstOrDefaultAsync(p => p.ParentId == parentId);
                            
                            if (parent?.User != null)
                            {
                                var enrolledClasses = await _context.Classes
                                    .Where(c => classIds.Contains(c.ClassId))
                                    .ToListAsync();
                                
                                foreach (var cls in enrolledClasses)
                                {
                                    var parentNotificationId = IdGenerator.GenerateNotificationId(_context);
                                    var parentNotification = new Notification
                                    {
                                        NotificationId = parentNotificationId,
                                        UserId = parent.UserId,
                                        Type = "Student Enrollment",
                                        Description = $"Your child {fullName} has been enrolled in {cls.ClassName}",
                                        RelatedEntityId = cls.ClassId,
                                        AffectedEntityId = studentId,
                                        Status = "unread",
                                        CreatedDate = DateTime.Now
                                    };
                                    _context.Notifications.Add(parentNotification);
                                }
                                await _context.SaveChangesAsync();
                            }
                        }
                    }

                    // Notify admins about new student registration
                    var adminUsers = await _context.Users.Where(u => u.UserType == "Admin").ToListAsync();
                    var enrolledClassIds = classIds != null && classIds.Any() ? string.Join(",", classIds) : null;
                    foreach (var admin in adminUsers)
                    {
                        var adminNotificationId = IdGenerator.GenerateNotificationId(_context);
                        var adminNotification = new Notification
                        {
                            NotificationId = adminNotificationId,
                            UserId = admin.UserId,
                            Type = "Student Registration",
                            Description = $"New student registered: {fullName} ({email}) with {enrolledCount} class enrollment(s)",
                            RelatedEntityId = studentId,
                            AffectedEntityId = enrolledClassIds,
                            Status = "unread",
                            CreatedDate = DateTime.Now
                        };
                        _context.Notifications.Add(adminNotification);
                    }
                    await _context.SaveChangesAsync();

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
            ViewBag.Title = _localization["AddNewStudent"];
            ViewBag.Localization = _localization;
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

            // Filter out unenrolled classes
            if (student.Enrollments != null)
            {
                student.Enrollments = student.Enrollments
                    .Where(e => e.UnenrolledDate == null)
                    .ToList();
            }

            ViewBag.ActiveMenu = "StudentManagement";
            ViewBag.Title = _localization["EditStudent"];
            ViewBag.Localization = _localization;
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
                            // Don't fail the operation if upload fails - use default
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
                    
                    int addedCount = 0;
                    int removedCount = 0;
                    var newlyEnrolledClassIds = new List<string>();

                    // Handle removal of enrollments - use ExecuteUpdate to avoid tracking conflicts
                    if (!string.IsNullOrEmpty(removeClassIds))
                    {
                        var classIdsToRemove = removeClassIds.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                        
                        // Get enrollments to remove without tracking
                        var enrollmentsToRemove = await _context.Enrollments
                            .AsNoTracking()
                            .Where(e => e.StudentId == studentId && 
                                       classIdsToRemove.Contains(e.ClassId) && 
                                       e.UnenrolledDate == null)
                            .Select(e => new { e.EnrollmentId, e.ClassId })
                            .ToListAsync();

                        foreach (var enrollment in enrollmentsToRemove)
                        {
                            // Use ExecuteUpdate to update without tracking
                            await _context.Enrollments
                                .Where(e => e.EnrollmentId == enrollment.EnrollmentId)
                                .ExecuteUpdateAsync(s => s.SetProperty(e => e.UnenrolledDate, DateTime.Now));

                            // Decrease class current capacity using ExecuteUpdate to avoid tracking
                            await _context.Classes
                                .Where(c => c.ClassId == enrollment.ClassId && c.CurrentCapacity > 0)
                                .ExecuteUpdateAsync(s => s.SetProperty(c => c.CurrentCapacity, c => c.CurrentCapacity - 1));

                            removedCount++;
                        }
                    }

                    // Handle addition of new enrollments
                    if (classIds != null && classIds.Any())
                    {
                        foreach (var classId in classIds)
                        {
                            // Check if already enrolled in this class
                            var isAlreadyEnrolled = await _context.Enrollments
                                .AsNoTracking()
                                .AnyAsync(e => e.StudentId == studentId && 
                                             e.ClassId == classId && 
                                             e.UnenrolledDate == null);

                            if (!isAlreadyEnrolled)
                            {
                                // Generate unique enrollment ID
                                var enrollmentId = IdGenerator.GenerateEnrollmentId(_context);
                                
                                // Add new enrollment
                                var enrollment = new Enrollment
                                {
                                    EnrollmentId = enrollmentId,
                                    StudentId = studentId,
                                    ClassId = classId,
                                    EnrolledDate = DateTime.Now,
                                    UnenrolledDate = null
                                };
                                _context.Enrollments.Add(enrollment);

                                // Update class current capacity
                                var classToUpdate = await _context.Classes.FindAsync(classId);
                                if (classToUpdate != null)
                                {
                                    classToUpdate.CurrentCapacity++;
                                }

                                // Track newly enrolled class
                                newlyEnrolledClassIds.Add(classId);
                                addedCount++;
                            }
                        }
                    }
                    
                    // Send notifications to teachers and parents about student enrollments
                    if (addedCount > 0 && newlyEnrolledClassIds.Any())
                    {
                        var classesWithTeachers = await _context.Classes
                            .Include(c => c.Teacher)
                            .ThenInclude(t => t.User)
                            .Where(c => newlyEnrolledClassIds.Contains(c.ClassId))
                            .ToListAsync();
                        
                        // Get student with parent info
                        var studentWithParent = await _context.Students
                            .Include(s => s.Parent)
                            .ThenInclude(p => p.User)
                            .FirstOrDefaultAsync(s => s.StudentId == studentId);
                        
                        foreach (var cls in classesWithTeachers)
                        {
                            // Notify teacher
                            if (cls.Teacher?.User != null)
                            {
                                var notificationId = IdGenerator.GenerateNotificationId(_context);
                                var teacherNotification = new Notification
                                {
                                    NotificationId = notificationId,
                                    UserId = cls.Teacher.UserId,
                                    Type = "Student Enrollment",
                                    Description = $"New student {fullName} has been enrolled in your class {cls.ClassName}",
                                    RelatedEntityId = cls.ClassId,
                                    AffectedEntityId = student.StudentId,
                                    Status = "unread",
                                    CreatedDate = DateTime.Now
                                };
                                _context.Notifications.Add(teacherNotification);
                            }
                            
                            // Notify parent
                            if (studentWithParent?.Parent?.User != null)
                            {
                                var parentNotificationId = IdGenerator.GenerateNotificationId(_context);
                                var parentNotification = new Notification
                                {
                                    NotificationId = parentNotificationId,
                                    UserId = studentWithParent.Parent.UserId,
                                    Type = "Student Enrollment",
                                    Description = $"Your child {fullName} has been enrolled in {cls.ClassName}",
                                    RelatedEntityId = cls.ClassId,
                                    AffectedEntityId = student.StudentId,
                                    Status = "unread",
                                    CreatedDate = DateTime.Now
                                };
                                _context.Notifications.Add(parentNotification);
                            }
                            
                            // Check capacity alert for admin
                            await CheckAndNotifyClassCapacity(cls.ClassId);
                        }
                    }
                    
                    // Notify parent about child unenrollment
                    if (removedCount > 0 && !string.IsNullOrEmpty(removeClassIds))
                    {
                        var studentWithParent = await _context.Students
                            .Include(s => s.Parent)
                            .ThenInclude(p => p.User)
                            .FirstOrDefaultAsync(s => s.StudentId == studentId);
                        
                        if (studentWithParent?.Parent?.User != null)
                        {
                            var removedClassIdList = removeClassIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
                            var removedClasses = await _context.Classes
                                .Where(c => removedClassIdList.Contains(c.ClassId))
                                .ToListAsync();
                            
                            foreach (var cls in removedClasses)
                            {
                                var parentNotificationId = IdGenerator.GenerateNotificationId(_context);
                                var parentNotification = new Notification
                                {
                                    NotificationId = parentNotificationId,
                                    UserId = studentWithParent.Parent.UserId,
                                    Type = "Student Unenrollment",
                                    Description = $"Your child {fullName} has been removed from {cls.ClassName}",
                                    RelatedEntityId = cls.ClassId,
                                    AffectedEntityId = student.StudentId,
                                    Status = "unread",
                                    CreatedDate = DateTime.Now
                                };
                                _context.Notifications.Add(parentNotification);
                            }
                        }
                    }

                    // Save all changes (student info + enrollments + notifications) in one transaction
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

            // Reload for display if validation fails
            ViewBag.Parents = await _context.Parents.Include(p => p.User).ToListAsync();
            ViewBag.Classes = await _context.Classes.ToListAsync();
            
            // Reload student with enrollments for display
            student = await _context.Students
                .Include(s => s.User)
                .Include(s => s.Enrollments)
                    .ThenInclude(e => e.Class)
                .FirstOrDefaultAsync(s => s.StudentId == studentId);
            
            // Filter out unenrolled classes
            if (student?.Enrollments != null)
            {
                student.Enrollments = student.Enrollments
                    .Where(e => e.UnenrolledDate == null)
                    .ToList();
            }
                
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

            // Filter out unenrolled classes (where UnenrolledDate is set)
            if (student.Enrollments != null)
            {
                student.Enrollments = student.Enrollments
                    .Where(e => e.UnenrolledDate == null)
                    .ToList();
            }

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
            // Calculate attendance rate with Present and Leave as attended
            var totalPresentIncludingLeave = presentCount + leaveCount;
            var attendanceRate = totalAttendance > 0 ? Math.Round((decimal)totalPresentIncludingLeave / totalAttendance * 100, 1) : 0;

            ViewBag.TotalEnrollments = student.Enrollments?.Count ?? 0;
            ViewBag.TotalAttendance = totalAttendance;
            ViewBag.PresentCount = totalPresentIncludingLeave; // Show combined count
            ViewBag.AbsentCount = absentCount;
            ViewBag.LeaveCount = leaveCount;
            ViewBag.AttendanceRate = attendanceRate;
            ViewBag.YearsSinceEnrollment = student.EnrollmentDate.HasValue
                ? Math.Round((DateTime.Now - student.EnrollmentDate.Value).TotalDays / 365.25, 1)
                : 0;
            ViewBag.Age = student.DateOfBirth.HasValue
                ? DateTime.Now.Year - student.DateOfBirth.Value.Year
                : 0;

            ViewBag.ActiveMenu = "StudentManagement";
            ViewBag.Title = _localization["StudentDetails"];
            ViewBag.Localization = _localization;

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
            ViewBag.Title = _localization["DeleteStudent"];
            ViewBag.Localization = _localization;

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

                    // Set UnenrolledDate for all active enrollments before deletion
                    if (student.Enrollments != null && student.Enrollments.Any())
                    {
                        foreach (var enrollment in student.Enrollments.Where(e => e.UnenrolledDate == null))
                        {
                            enrollment.UnenrolledDate = DateTime.Now;
                            _context.Update(enrollment);
                            
                            // Decrease capacity for all enrolled classes
                            var classToUpdate = await _context.Classes.FindAsync(enrollment.ClassId);
                            if (classToUpdate != null && classToUpdate.CurrentCapacity > 0)
                            {
                                classToUpdate.CurrentCapacity--;
                            }
                        }
                    }

                    _context.Users.Remove(student.User); // Cascade delete will remove student and related data
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Student '{studentName}' deleted successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Student not found.";
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
            ViewBag.Title = _localization["TeacherManagement"];
            ViewBag.Localization = _localization;

            var teachers = await _context.Teachers.Include(t => t.User).ToListAsync();
            return View(teachers);
        }

        public async Task<IActionResult> TeacherCreate()
        {
            ViewBag.ActiveMenu = "TeacherManagement";
            ViewBag.Title = "Create Teacher";
            
            // Fetch subjects from database for dropdown
            ViewBag.Subjects = await _context.Subjects.OrderBy(s => s.SubjectName).ToListAsync();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TeacherCreate(
            string fullName, string email,
            string phoneNumber, string subjectTeach, DateTime? hireDate,
            string title, string education, string? skill, string? bio,
            DateTime? dateOfBirth, string gender, string? status,
            IFormFile? profilePicture)
        {
            // Manual validation for required fields
            if (string.IsNullOrWhiteSpace(fullName))
                ModelState.AddModelError("fullName", "Full name is required");

            if (string.IsNullOrWhiteSpace(email))
                ModelState.AddModelError("email", "Email is required");
                
            if (string.IsNullOrWhiteSpace(phoneNumber))
                ModelState.AddModelError("phoneNumber", "Phone number is required");
                
            if (string.IsNullOrWhiteSpace(subjectTeach))
                ModelState.AddModelError("subjectTeach", "Subject is required");
                
            if (string.IsNullOrWhiteSpace(title))
                ModelState.AddModelError("title", "Title is required");
                
            if (string.IsNullOrWhiteSpace(education))
                ModelState.AddModelError("education", "Highest education is required");
                
            if (string.IsNullOrWhiteSpace(gender))
                ModelState.AddModelError("gender", "Gender is required");
                
            if (!dateOfBirth.HasValue)
                ModelState.AddModelError("dateOfBirth", "Date of birth is required");

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
                        HireDate = hireDate!.Value,
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
                    
                    // Notify admins about new teacher registration
                    var adminUsers = await _context.Users.Where(u => u.UserType == "Admin").ToListAsync();
                    foreach (var admin in adminUsers)
                    {
                        var adminNotificationId = IdGenerator.GenerateNotificationId(_context);
                        var adminNotification = new Notification
                        {
                            NotificationId = adminNotificationId,
                            UserId = admin.UserId,
                            Type = "Teacher Registration",
                            Description = $"New teacher registered: {fullName} ({email}) - {title}, {subjectTeach}",
                            RelatedEntityId = userId,
                            Status = "unread",
                            CreatedDate = DateTime.Now
                        };
                        _context.Notifications.Add(adminNotification);
                    }
                    await _context.SaveChangesAsync();
                    
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
            ViewBag.Title = _localization["CreateTeacher"];
            ViewBag.Localization = _localization;
            ViewBag.FullName = fullName;
            ViewBag.Email = email;
            ViewBag.PhoneNumber = phoneNumber;
            ViewBag.SubjectTeach = subjectTeach;
            ViewBag.HireDate = hireDate?.ToString("yyyy-MM-dd");
            ViewBag.TeacherTitle = title;
            ViewBag.Education = education;
            ViewBag.Skill = skill;
            ViewBag.Bio = bio;
            ViewBag.DateOfBirth = dateOfBirth?.ToString("yyyy-MM-dd");
            ViewBag.Gender = gender;
            ViewBag.Status = status;
            
            // Re-fetch subjects for dropdown
            ViewBag.Subjects = await _context.Subjects.OrderBy(s => s.SubjectName).ToListAsync();

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
            ViewBag.Title = _localization["EditTeacher"];
            ViewBag.Localization = _localization;
            
            // Fetch subjects from database for dropdown
            ViewBag.Subjects = await _context.Subjects.OrderBy(s => s.SubjectName).ToListAsync();

            return View(teacher);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TeacherEdit(
            string teacherId, string fullName, string email,
            string phoneNumber, string subjectTeach, DateTime? hireDate,
            string title, string education, string? skill, string? bio,
            DateTime? dateOfBirth, string gender, string? status,
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
                
            if (string.IsNullOrWhiteSpace(phoneNumber))
                ModelState.AddModelError("phoneNumber", "Phone number is required");
                
            if (string.IsNullOrWhiteSpace(subjectTeach))
                ModelState.AddModelError("subjectTeach", "Subject is required");
                
            if (string.IsNullOrWhiteSpace(title))
                ModelState.AddModelError("title", "Title is required");
                
            if (string.IsNullOrWhiteSpace(education))
                ModelState.AddModelError("education", "Highest education is required");
                
            if (string.IsNullOrWhiteSpace(gender))
                ModelState.AddModelError("gender", "Gender is required");
                
            if (!dateOfBirth.HasValue)
                ModelState.AddModelError("dateOfBirth", "Date of birth is required");

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
                    teacher.User.Status = status!;
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
            ViewBag.Title = _localization["EditTeacher"];
            ViewBag.Localization = _localization;
            
            // Re-fetch subjects for dropdown
            ViewBag.Subjects = await _context.Subjects.OrderBy(s => s.SubjectName).ToListAsync();
            
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
            var leaveCount = attendanceStats.FirstOrDefault(s => s.Status == "Leave")?.Count ?? 0;

            ViewBag.TotalClassesAssigned = teacher.Classes?.Count ?? 0;
            ViewBag.TotalStudentsTeaching = teacher.Classes?.Sum(c => c.CurrentCapacity) ?? 0;
            ViewBag.TotalAttendanceMarked = totalAttendanceMarked;
            ViewBag.PresentCount = presentCount;
            ViewBag.AbsentCount = absentCount;
            ViewBag.LeaveCount = leaveCount;
            ViewBag.YearsOfService = teacher.HireDate.HasValue
                ? Math.Round((DateTime.Now - teacher.HireDate.Value).TotalDays / 365.25, 1)
                : 0;

            ViewBag.ActiveMenu = "TeacherManagement";
            ViewBag.Title = _localization["TeacherDetails"];
            ViewBag.Localization = _localization;

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
            ViewBag.Title = _localization["DeleteTeacher"];
            ViewBag.Localization = _localization;
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
            ViewBag.Title = _localization["ParentManagement"];
            ViewBag.Localization = _localization;

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
            string phoneNumber, string address, DateTime? dateOfBirth, string gender,
            IFormFile? profilePicture)
        {
            // Manual validation for required fields
            if (string.IsNullOrWhiteSpace(fullName))
                ModelState.AddModelError("fullName", "Full name is required");

            if (string.IsNullOrWhiteSpace(email))
                ModelState.AddModelError("email", "Email is required");
                
            if (string.IsNullOrWhiteSpace(phoneNumber))
                ModelState.AddModelError("phoneNumber", "Phone number is required");
                
            if (string.IsNullOrWhiteSpace(address))
                ModelState.AddModelError("address", "Address is required");
                
            if (!dateOfBirth.HasValue)
                ModelState.AddModelError("dateOfBirth", "Date of birth is required");
                
            if (string.IsNullOrWhiteSpace(gender))
                ModelState.AddModelError("gender", "Gender is required");

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
                    
                    // Notify admins about new parent registration
                    var adminUsers = await _context.Users.Where(u => u.UserType == "Admin").ToListAsync();
                    foreach (var admin in adminUsers)
                    {
                        var adminNotificationId = IdGenerator.GenerateNotificationId(_context);
                        var adminNotification = new Notification
                        {
                            NotificationId = adminNotificationId,
                            UserId = admin.UserId,
                            Type = "Parent Registration",
                            Description = $"New parent registered: {fullName} ({email})",
                            RelatedEntityId = userId,
                            Status = "unread",
                            CreatedDate = DateTime.Now
                        };
                        _context.Notifications.Add(adminNotification);
                    }
                    await _context.SaveChangesAsync();
                    
                    TempData["SuccessMessage"] = $"Parent '{fullName}' added successfully! A temporary password has been sent to {email}.";
                    return RedirectToAction(nameof(ParentIndex));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error saving parent: {ex.Message}");
                }
            }

            ViewBag.ActiveMenu = "ParentManagement";
            ViewBag.Title = _localization["CreateParent"];
            ViewBag.Localization = _localization;
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
            ViewBag.Title = _localization["EditParent"];
            ViewBag.Localization = _localization;

            return View(parent);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ParentEdit(string parentId, string fullName, string email, 
            string phoneNumber, string address, DateTime? dateOfBirth, string gender, string status,
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
                
            if (string.IsNullOrWhiteSpace(phoneNumber))
                ModelState.AddModelError("phoneNumber", "Phone number is required");
                
            if (string.IsNullOrWhiteSpace(address))
                ModelState.AddModelError("address", "Address is required");
                
            if (!dateOfBirth.HasValue)
                ModelState.AddModelError("dateOfBirth", "Date of birth is required");
                
            if (string.IsNullOrWhiteSpace(gender))
                ModelState.AddModelError("gender", "Gender is required");

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
            ViewBag.Title = _localization["EditParent"];
            ViewBag.Localization = _localization;
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
            ViewBag.Title = _localization["ParentDetails"];
            ViewBag.Localization = _localization;

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
            ViewBag.Title = _localization["DeleteParent"];
            ViewBag.Localization = _localization;

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
            ViewBag.Title = _localization["ClassManagement"];
            ViewBag.Localization = _localization;

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
            ViewBag.Title = _localization["CreateClass"];
            ViewBag.Localization = _localization;
            ViewBag.Teachers = await _context.Teachers.Include(t => t.User).ToListAsync();
            ViewBag.Subjects = await _context.Subjects.ToListAsync();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClassCreate(string className, string teacherId, string roomNumber,
            string day, string startTime, string endTime, string subjectId, int maxCapacity = 30, bool isActive = true)
        {
            // Manual validation for all required fields
            if (string.IsNullOrWhiteSpace(className))
                ModelState.AddModelError("className", "Class name is required");
                
            if (string.IsNullOrWhiteSpace(teacherId))
                ModelState.AddModelError("teacherId", "Teacher is required");
                
            if (string.IsNullOrWhiteSpace(subjectId))
                ModelState.AddModelError("subjectId", "Subject is required");
                
            if (string.IsNullOrWhiteSpace(roomNumber))
                ModelState.AddModelError("roomNumber", "Venue/Room is required");
                
            if (string.IsNullOrWhiteSpace(day))
                ModelState.AddModelError("day", "Day of week is required");
                
            if (string.IsNullOrWhiteSpace(startTime))
                ModelState.AddModelError("startTime", "Start time is required");
                
            if (string.IsNullOrWhiteSpace(endTime))
                ModelState.AddModelError("endTime", "End time is required");

            if (maxCapacity < 1)
                ModelState.AddModelError("maxCapacity", "Maximum capacity must be at least 1");
                
            // Parse time strings to TimeSpan for validation
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
            
            // Validate that end time is after start time
            if (parsedStartTime.HasValue && parsedEndTime.HasValue && parsedEndTime.Value <= parsedStartTime.Value)
            {
                ModelState.AddModelError("endTime", "End time must be after start time");
            }
            
            // Check for schedule conflicts: same day, same time, same venue
            if (!string.IsNullOrWhiteSpace(day) && !string.IsNullOrWhiteSpace(roomNumber) && parsedStartTime.HasValue && parsedEndTime.HasValue)
            {
                var conflictingClasses = await _context.Classes
                    .Where(c => c.Day == day && c.RoomNumber == roomNumber && c.StartTime.HasValue && c.EndTime.HasValue)
                    .ToListAsync();
                    
                foreach (var existingClass in conflictingClasses)
                {
                    // Check if time ranges overlap
                    if ((parsedStartTime.Value < existingClass.EndTime.Value && parsedEndTime.Value > existingClass.StartTime.Value))
                    {
                        ModelState.AddModelError("", $"Schedule conflict: {existingClass.ClassName} is already scheduled in {roomNumber} on {day} from {existingClass.StartTime.Value:hh\\:mm} to {existingClass.EndTime.Value:hh\\:mm}");
                        break;
                    }
                }
            }

            if (ModelState.IsValid)
            {
                var classId = IdGenerator.GenerateClassId(_context);

                var @class = new Class
                {
                    ClassId = classId,
                    ClassName = className,
                    TeacherId = teacherId,
                    SubjectId = subjectId,
                    RoomNumber = roomNumber,
                    Day = day,
                    StartTime = parsedStartTime,
                    EndTime = parsedEndTime,
                    MaxCapacity = maxCapacity,
                    CurrentCapacity = 0,
                    IsActive = isActive
                };
                _context.Classes.Add(@class);

                // Create initial active history record if class is active
                if (isActive)
                {
                    var historyCount = await _context.ClassActiveHistories.CountAsync();
                    var history = new ClassActiveHistory
                    {
                        HistoryId = $"CAH{(historyCount + 1):D5}",
                        ClassId = classId,
                        ActiveFrom = DateTime.Today,
                        ActiveTo = null,
                        CreatedDate = DateTime.Now
                    };
                    _context.ClassActiveHistories.Add(history);
                }

                // Send notification to teacher about class assignment
                if (!string.IsNullOrEmpty(teacherId))
                {
                    var teacher = await _context.Teachers
                        .Include(t => t.User)
                        .FirstOrDefaultAsync(t => t.TeacherId == teacherId);
                    
                    if (teacher != null)
                    {
                        var notificationId = IdGenerator.GenerateNotificationId(_context);
                        var teacherNotification = new Notification
                        {
                            NotificationId = notificationId,
                            UserId = teacher.UserId,
                            Type = "Class Assignment",
                            Description = $"You have been assigned to teach {className} on {day} from {parsedStartTime:hh\\:mm} to {parsedEndTime:hh\\:mm} at {roomNumber}",
                            Status = "unread",
                            CreatedDate = DateTime.Now
                        };
                        _context.Notifications.Add(teacherNotification);
                    }
                }

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
                .Include(c => c.Enrollments.Where(e => e.UnenrolledDate == null))
                    .ThenInclude(e => e.Student)
                    .ThenInclude(s => s.User)
                .Include(c => c.ActiveHistories)
                .FirstOrDefaultAsync(c => c.ClassId == id);

            if (@class == null) return NotFound();

            ViewBag.ActiveMenu = "ClassManagement";
            ViewBag.ActiveSubmenu = "Classes";
            ViewBag.Title = _localization["EditClass"];
            ViewBag.Localization = _localization;
            ViewBag.Teachers = await _context.Teachers.Include(t => t.User).ToListAsync();
            ViewBag.Subjects = await _context.Subjects.ToListAsync();
            ViewBag.Students = await _context.Students.Include(s => s.User).OrderBy(s => s.User.FullName).ToListAsync();

            return View(@class);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClassEdit(string classId, string className, string teacherId,
            string subjectId, string roomNumber, string day, string startTime, string endTime, int maxCapacity, bool isActive,
            List<string>? studentIds, string? removeStudentIds)
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

            // Calculate the new capacity considering student additions/removals
            int capacityAdjustment = 0;
            if (!string.IsNullOrEmpty(removeStudentIds))
            {
                var studentIdsToRemove = removeStudentIds.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                capacityAdjustment -= studentIdsToRemove.Count;
            }
            if (studentIds != null && studentIds.Any())
            {
                // Only count students who aren't already enrolled
                var alreadyEnrolledIds = await _context.Enrollments
                    .Where(e => e.ClassId == classId && studentIds.Contains(e.StudentId) && e.UnenrolledDate == null)
                    .Select(e => e.StudentId)
                    .ToListAsync();
                capacityAdjustment += studentIds.Count - alreadyEnrolledIds.Count;
            }
            
            var projectedCapacity = @class.CurrentCapacity + capacityAdjustment;
            
            if (maxCapacity < projectedCapacity)
                ModelState.AddModelError("maxCapacity", $"Maximum capacity cannot be less than projected enrollment ({projectedCapacity})");

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

                    int addedCount = 0;
                    int removedCount = 0;

                    // Handle removal of students - use ExecuteUpdate to avoid tracking conflicts
                    if (!string.IsNullOrEmpty(removeStudentIds))
                    {
                        var studentIdsToRemove = removeStudentIds.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                        
                        // Get enrollments to remove without tracking
                        var enrollmentsToRemove = await _context.Enrollments
                            .AsNoTracking()
                            .Where(e => e.ClassId == classId && 
                                       studentIdsToRemove.Contains(e.StudentId) && 
                                       e.UnenrolledDate == null)
                            .Select(e => e.EnrollmentId)
                            .ToListAsync();

                        foreach (var enrollmentId in enrollmentsToRemove)
                        {
                            // Use ExecuteUpdate to update without tracking
                            await _context.Enrollments
                                .Where(e => e.EnrollmentId == enrollmentId)
                                .ExecuteUpdateAsync(s => s.SetProperty(e => e.UnenrolledDate, DateTime.Now));

                            removedCount++;
                        }
                        
                        // Decrease class current capacity
                        @class.CurrentCapacity -= removedCount;
                    }

                    // Handle addition of new students
                    if (studentIds != null && studentIds.Any())
                    {
                        foreach (var studentId in studentIds)
                        {
                            // Check if already enrolled in this class
                            var isAlreadyEnrolled = await _context.Enrollments
                                .AsNoTracking()
                                .AnyAsync(e => e.ClassId == classId && 
                                             e.StudentId == studentId && 
                                             e.UnenrolledDate == null);

                            if (!isAlreadyEnrolled)
                            {
                                // Generate unique enrollment ID
                                var enrollmentId = IdGenerator.GenerateEnrollmentId(_context);
                                
                                // Add new enrollment
                                var enrollment = new Enrollment
                                {
                                    EnrollmentId = enrollmentId,
                                    StudentId = studentId,
                                    ClassId = classId,
                                    EnrolledDate = DateTime.Now,
                                    UnenrolledDate = null
                                };
                                _context.Enrollments.Add(enrollment);

                                addedCount++;
                            }
                        }
                        
                        // Increase class current capacity
                        @class.CurrentCapacity += addedCount;
                        
                        // Check capacity alert for admin after adding students
                        if (addedCount > 0)
                        {
                            await CheckAndNotifyClassCapacity(@class.ClassId);
                        }
                        
                        // Notify parents about their children's enrollment in this class
                        if (addedCount > 0 && studentIds != null && studentIds.Any())
                        {
                            var studentsWithParents = await _context.Students
                                .Include(s => s.User)
                                .Include(s => s.Parent)
                                .ThenInclude(p => p.User)
                                .Where(s => studentIds.Contains(s.StudentId))
                                .ToListAsync();
                            
                            foreach (var student in studentsWithParents)
                            {
                                // Check if this student was just enrolled (within last 5 minutes)
                                var wasJustEnrolled = await _context.Enrollments
                                    .AnyAsync(e => e.StudentId == student.StudentId && 
                                                  e.ClassId == classId && 
                                                  e.UnenrolledDate == null &&
                                                  e.EnrolledDate > DateTime.Now.AddMinutes(-5));
                                
                                if (wasJustEnrolled && student.Parent?.User != null)
                                {
                                    var parentNotificationId = IdGenerator.GenerateNotificationId(_context);
                                    var parentNotification = new Notification
                                    {
                                        NotificationId = parentNotificationId,
                                        UserId = student.Parent.UserId,
                                        Type = "Student Enrollment",
                                        Description = $"Your child {student.User.FullName} has been enrolled in {@class.ClassName}",
                                        RelatedEntityId = @class.ClassId,
                                        AffectedEntityId = student.StudentId,
                                        Status = "unread",
                                        CreatedDate = DateTime.Now
                                    };
                                    _context.Notifications.Add(parentNotification);
                                }
                            }
                        }
                    }

                    var wasActive = @class.IsActive;
                    var isNowActive = isActive;

                    if (wasActive != isNowActive)
                    {
                        if (isNowActive)
                        {
                            // Class is being activated - create new active period
                            var historyCount = await _context.ClassActiveHistories.CountAsync();
                            var history = new ClassActiveHistory
                            {
                                HistoryId = $"CAH{(historyCount + 1):D5}",
                                ClassId = classId,
                                ActiveFrom = DateTime.Today,
                                ActiveTo = null, // Still active
                                CreatedDate = DateTime.Now
                            };
                            _context.ClassActiveHistories.Add(history);
                        }
                        else
                        {
                            // Class is being deactivated - close the current active period and unenroll all students
                            var currentActivePeriod = await _context.ClassActiveHistories
                                .Where(h => h.ClassId == classId && h.ActiveTo == null)
                                .OrderByDescending(h => h.ActiveFrom)
                                .FirstOrDefaultAsync();

                            if (currentActivePeriod != null)
                            {
                                currentActivePeriod.ActiveTo = DateTime.Today;
                                _context.Update(currentActivePeriod);
                            }

                            // Unenroll all remaining students from this class
                            var remainingEnrollments = await _context.Enrollments
                                .Where(e => e.ClassId == classId && e.UnenrolledDate == null)
                                .ToListAsync();
                            foreach (var enrollment in remainingEnrollments)
                            {
                                enrollment.UnenrolledDate = DateTime.Now;
                                _context.Update(enrollment);
                            }
                            
                            // Notify parents about unenrollment when class is deactivated
                            if (remainingEnrollments.Any())
                            {
                                var studentIdsToNotify = remainingEnrollments.Select(e => e.StudentId).ToList();
                                var studentsWithParents = await _context.Students
                                    .Include(s => s.User)
                                    .Include(s => s.Parent)
                                    .ThenInclude(p => p.User)
                                    .Where(s => studentIdsToNotify.Contains(s.StudentId))
                                    .ToListAsync();
                                
                                foreach (var student in studentsWithParents)
                                {
                                    if (student.Parent?.User != null)
                                    {
                                        var parentNotificationId = IdGenerator.GenerateNotificationId(_context);
                                        var parentNotification = new Notification
                                        {
                                            NotificationId = parentNotificationId,
                                            UserId = student.Parent.UserId,
                                            Type = "Student Unenrollment",
                                            Description = $"Your child {student.User.FullName} has been unenrolled from {@class.ClassName} (class deactivated)",
                                            RelatedEntityId = @class.ClassId,
                                            AffectedEntityId = student.StudentId,
                                            Status = "unread",
                                            CreatedDate = DateTime.Now
                                        };
                                        _context.Notifications.Add(parentNotification);
                                    }
                                }
                            }
                            
                            // Reset current capacity to 0
                            @class.CurrentCapacity = 0;
                        }
                    }

                    // Check if teacher assignment changed
                    var oldTeacherId = @class.TeacherId;
                    var teacherChanged = oldTeacherId != teacherId;

                    // Update class information
                    @class.ClassName = className;
                    @class.TeacherId = string.IsNullOrEmpty(teacherId) ? null : teacherId;
                    @class.SubjectId = string.IsNullOrEmpty(subjectId) ? null : subjectId;
                    @class.RoomNumber = roomNumber;
                    @class.Day = day;
                    @class.StartTime = parsedStartTime;
                    @class.EndTime = parsedEndTime;
                    @class.MaxCapacity = maxCapacity;
                    @class.IsActive = isActive;

                    _context.Update(@class);
                    
                    // Prepare all notifications to be added in batch
                    var notificationsToAdd = new List<Notification>();

                    // Send notification to new teacher if assigned
                    if (teacherChanged && !string.IsNullOrEmpty(teacherId))
                    {
                        var newTeacher = await _context.Teachers
                            .Include(t => t.User)
                            .FirstOrDefaultAsync(t => t.TeacherId == teacherId);
                        
                        if (newTeacher != null)
                        {
                            notificationsToAdd.Add(new Notification
                            {
                                NotificationId = "", // Will be set below
                                UserId = newTeacher.UserId,
                                Type = "Class Assignment",
                                Description = $"You have been assigned to teach {className} on {day} from {parsedStartTime:hh\\:mm} to {parsedEndTime:hh\\:mm} at {roomNumber}",
                                Status = "unread",
                                CreatedDate = DateTime.Now
                            });
                        }
                    }

                    // Send notifications to students about enrollment
                    if (addedCount > 0 && studentIds != null)
                    {
                        var newlyEnrolledStudents = await _context.Enrollments
                            .Include(e => e.Student)
                            .ThenInclude(s => s.User)
                            .Where(e => studentIds.Contains(e.StudentId) && e.ClassId == classId && 
                                   e.EnrolledDate > DateTime.Now.AddMinutes(-5))
                            .Select(e => e.Student)
                            .ToListAsync();
                        
                        foreach (var student in newlyEnrolledStudents)
                        {
                            if (student?.User != null)
                            {
                                notificationsToAdd.Add(new Notification
                                {
                                    NotificationId = "",
                                    UserId = student.UserId,
                                    Type = "Class Enrollment",
                                    Description = $"You have been enrolled in {className} ({day} {parsedStartTime:hh\\:mm}-{parsedEndTime:hh\\:mm})",
                                    Status = "unread",
                                    CreatedDate = DateTime.Now
                                });
                            }
                        }
                    }

                    // Notify teacher about student enrollments if teacher is assigned
                    if ((addedCount > 0 || removedCount > 0) && !string.IsNullOrEmpty(@class.TeacherId))
                    {
                        var teacher = await _context.Teachers
                            .Include(t => t.User)
                            .FirstOrDefaultAsync(t => t.TeacherId == @class.TeacherId);
                        
                        if (teacher != null)
                        {
                            var description = addedCount > 0 && removedCount > 0 
                                ? $"{addedCount} student(s) enrolled and {removedCount} student(s) unenrolled from your class {className}"
                                : addedCount > 0 
                                ? $"{addedCount} student(s) enrolled in your class {className}"
                                : $"{removedCount} student(s) unenrolled from your class {className}";
                            
                            // Get affected student IDs
                            string affectedIds = "";
                            if (addedCount > 0 && studentIds != null)
                            {
                                affectedIds = string.Join(",", studentIds);
                            }
                            else if (removedCount > 0 && !string.IsNullOrEmpty(removeStudentIds))
                            {
                                affectedIds = removeStudentIds;
                            }
                            
                            notificationsToAdd.Add(new Notification
                            {
                                NotificationId = "",
                                UserId = teacher.UserId,
                                Type = addedCount > 0 ? "Student Enrollment" : "Student Unenrollment",
                                Description = description,
                                RelatedEntityId = @class.ClassId,
                                AffectedEntityId = affectedIds,
                                Status = "unread",
                                CreatedDate = DateTime.Now
                            });
                        }
                    }

                    // Add all notifications with proper IDs
                    foreach (var notification in notificationsToAdd)
                    {
                        notification.NotificationId = IdGenerator.GenerateNotificationId(_context);
                        _context.Notifications.Add(notification);
                    }

                    await _context.SaveChangesAsync();

                    var message = $"Class '{className}' updated successfully!";
                    if (addedCount > 0 || removedCount > 0)
                    {
                        message += $" Added {addedCount} student(s), removed {removedCount} student(s).";
                    }
                    
                    TempData["SuccessMessage"] = message;
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
            ViewBag.Title = _localization["EditClass"];
            ViewBag.Localization = _localization;
            ViewBag.Teachers = await _context.Teachers.Include(t => t.User).ToListAsync();
            ViewBag.Subjects = await _context.Subjects.ToListAsync();
            ViewBag.Students = await _context.Students.Include(s => s.User).OrderBy(s => s.User.FullName).ToListAsync();
            
            // Reload class with enrollments for display
            @class = await _context.Classes
                .Include(c => c.Teacher)
                    .ThenInclude(t => t.User)
                .Include(c => c.Subject)
                .Include(c => c.Enrollments.Where(e => e.UnenrolledDate == null))
                    .ThenInclude(e => e.Student)
                    .ThenInclude(s => s.User)
                .FirstOrDefaultAsync(c => c.ClassId == classId);
            
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
            ViewBag.Title = _localization["ClassDetails"];
            ViewBag.Localization = _localization;

            return View(@class);
        }


        public async Task<IActionResult> ScheduleIndex()
        {
            ViewBag.ActiveMenu = "ClassManagement";
            ViewBag.ActiveSubmenu = "Schedule";
            ViewBag.Title = _localization["ClassSchedule"];
            ViewBag.Localization = _localization;

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
            ViewBag.Title = _localization["SubjectManagement"];
            ViewBag.Localization = _localization;

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
            ViewBag.Title = _localization["CreateSubject"];
            ViewBag.Localization = _localization;

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
            ViewBag.Title = _localization["CreateSubject"];
            ViewBag.Localization = _localization;
            return View();
        }

        public async Task<IActionResult> SubjectEdit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var subject = await _context.Subjects
                .Include(s => s.Classes)
                .FirstOrDefaultAsync(s => s.SubjectId == id);

            if (subject == null)
                return NotFound();

            ViewBag.ActiveMenu = "ClassManagement";
            ViewBag.ActiveSubmenu = "Subjects";
            ViewBag.Title = _localization["EditSubject"];
            ViewBag.Localization = _localization;

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
            ViewBag.Title = _localization["EditSubject"];
            ViewBag.Localization = _localization;
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
            ViewBag.Title = _localization["SubjectDetails"];
            ViewBag.Localization = _localization;

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
            ViewBag.Title = _localization["DeleteSubject"];
            ViewBag.Localization = _localization;

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

        // ==================== ATTENDANCE MANAGEMENT ====================        
        // Generate PIN for attendance session
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
                var classEntity = await _context.Classes
                    .Include(c => c.Teacher)
                    .FirstOrDefaultAsync(c => c.ClassId == classId);

                if (classEntity == null)
                {
                    return Json(new { success = false, message = "Class not found" });
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
                var existingSession = await _context.AttendanceSessions
                    .FirstOrDefaultAsync(s => s.ClassId == classId && 
                                            s.IsActive && 
                                            s.CreatedDate.Date == DateTime.Today);

                if (existingSession != null)
                {
                    return Json(new { success = false, message = "PIN has already been generated for this class today" });
                }

                // Generate random 6-digit PIN
                var pinCode = IdGenerator.GenerateAttendancePinCode(_context);

                // Create attendance session
                var sessionId = IdGenerator.GenerateSessionId(_context);

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

                _context.AttendanceSessions.Add(session);
                await _context.SaveChangesAsync();

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

        // Take Attendance with PIN Code
        public async Task<IActionResult> AttendanceTake(DateTime? selectedDate)
        {
            ViewBag.ActiveMenu = "AttendanceManagement";
            ViewBag.ActiveSubmenu = "Take";
            ViewBag.Title = _localization["TakeAttendance"];
            ViewBag.Localization = _localization;

            // Use selected date or default to today
            var targetDate = selectedDate ?? DateTime.Today;
            ViewBag.SelectedDate = targetDate;
            ViewBag.SelectedDay = targetDate.DayOfWeek.ToString();

            // Load all classes with their active histories
            var allClasses = await _context.Classes
                .Include(c => c.Teacher)
                .ThenInclude(t => t.User)
                .Include(c => c.Enrollments)
                .ThenInclude(e => e.Student)
                .Include(c => c.ActiveHistories)
                .OrderBy(c => c.ClassName)
                .ToListAsync();

            // Filter classes based on whether they were active on the selected date
            // AND filter enrollments to only show students enrolled on that date
            var filteredClasses = allClasses
                .Where(c => WasClassActiveOnDate(c, targetDate))
                .Select(c => new Class
                {
                    ClassId = c.ClassId,
                    ClassName = c.ClassName,
                    Day = c.Day,
                    StartTime = c.StartTime,
                    EndTime = c.EndTime,
                    RoomNumber = c.RoomNumber,
                    Teacher = c.Teacher,
                    // Filter enrollments to only those active on the target date
                    Enrollments = c.Enrollments
                        .Where(e => e.EnrolledDate.Date <= targetDate.Date && 
                                   (e.UnenrolledDate == null || e.UnenrolledDate.Value.Date > targetDate.Date))
                        .ToList()
                })
                .ToList();

            ViewBag.Classes = filteredClasses;

            // Load ONLY sessions created on the selected date
            ViewBag.Sessions = await _context.AttendanceSessions
                .Include(s => s.Class)
                .Where(s => s.IsActive && s.CreatedDate.Date == targetDate.Date)
                .ToListAsync();

            return View();
        }

        private bool WasClassActiveOnDate(Class classEntity, DateTime date)
        {
            // If no active histories exist, fall back to current IsActive status
            // (for classes created before this feature was implemented)
            if (classEntity.ActiveHistories == null || !classEntity.ActiveHistories.Any())
            {
                return classEntity.IsActive;
            }

            // Check if the date falls within any active period
            return classEntity.ActiveHistories.Any(h =>
                h.ActiveFrom.Date <= date.Date &&
                (h.ActiveTo == null || h.ActiveTo.Value.Date >= date.Date));
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
                .Include(c => c.ActiveHistories)
                .FirstOrDefaultAsync(c => c.ClassId == id);

            if (classEntity == null) return NotFound();

            ViewBag.ActiveMenu = "AttendanceManagement";
            ViewBag.ActiveSubmenu = "Take";

            DateTime targetDate = selectedDate ?? DateTime.Today;
            bool wasActiveOnDate = WasClassActiveOnDate(classEntity, targetDate);

            ViewBag.WasActiveOnDate = wasActiveOnDate;
            ViewBag.SelectedDate = targetDate;

            // Load the session created on the selected date for this class
            var sessionOnSelectedDate = await _context.AttendanceSessions
                .FirstOrDefaultAsync(s => s.IsActive && 
                           s.ClassId == id && 
                           s.CreatedDate.Date == targetDate.Date);

            // Filter enrollments to show students who:
            // 1. Were enrolled on or before the selected date, AND
            // 2. Either still enrolled (UnenrolledDate == null) OR
            // 3. Were unenrolled on a different date OR
            // 4. Were unenrolled on the same date but AFTER the class started
            var classStartDateTime = classEntity.StartTime.HasValue 
                ? targetDate.Date.Add(classEntity.StartTime.Value) 
                : targetDate.Date;
            
            var filteredEnrollments = classEntity.Enrollments
                .Where(e => e.EnrolledDate.Date <= targetDate.Date && 
                           (e.UnenrolledDate == null || 
                            e.UnenrolledDate.Value.Date > targetDate.Date ||
                            (e.UnenrolledDate.Value.Date == targetDate.Date && e.UnenrolledDate.Value > classStartDateTime)))
                .ToList();
            
            ViewBag.FilteredEnrollments = filteredEnrollments;

            // Load ONLY the session created on the selected date for this class
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
            var todayAttendances = await _context.Attendances
                .Where(a => a.ClassId == id && a.Date.Date == targetDate.Date)
                .ToListAsync();

            // Create attendance summary: include marked attendance and add "Not Marked" for students without records
            var attendanceSummary = new List<AttendanceSummaryDto>();
            var markedStudentIds = todayAttendances.Select(a => a.StudentId).ToHashSet();

            // Add all marked attendances
            foreach (var att in todayAttendances)
            {
                attendanceSummary.Add(new AttendanceSummaryDto
                {
                    AttendanceId = att.AttendanceId,
                    StudentId = att.StudentId,
                    ClassId = att.ClassId,
                    Date = att.Date,
                    Status = att.Status,
                    TakenOn = att.TakenOn,
                    MarkedByTeacherId = att.MarkedByTeacherId,
                    Flag = att.Flag
                });
            }

            // Add "Not Marked" for students without attendance records
            foreach (var enrollment in filteredEnrollments)
            {
                if (!markedStudentIds.Contains(enrollment.StudentId))
                {
                    attendanceSummary.Add(new AttendanceSummaryDto
                    {
                        AttendanceId = null,
                        StudentId = enrollment.StudentId,
                        ClassId = id,
                        Date = targetDate,
                        Status = "Not Marked",
                        TakenOn = null,
                        MarkedByTeacherId = null,
                        Flag = false
                    });
                }
            }

            ViewBag.TodayAttendances = attendanceSummary.OrderBy(a => a.StudentId).ToList();

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
                var absentStudents = new List<(string studentId, string classId, DateTime date)>();

                foreach (var att in request.Attendances)
                {
                    // Skip "Not Marked" status - don't save it to database
                    if (att.Status == "Not Marked")
                    {
                        continue;
                    }

                    // Check if attendance already exists for this student on this date
                    var existing = await _context.Attendances
                        .FirstOrDefaultAsync(a => a.StudentId == att.StudentId &&
                                                  a.ClassId == request.ClassId &&
                                                  a.Date.Date == selectedDate.Date);

                    // Skip if student is on Leave - cannot change leave status
                    if (existing != null && existing.Status == "Leave")
                    {
                        continue;
                    }

                    if (existing != null)
                    {
                        // Update existing attendance
                        if (existing.Status != att.Status)
                        {
                            existing.Status = att.Status;
                            existing.TakenOn = DateTime.Now;
                            _context.Update(existing);
                            markedCount++;
                            
                            // Track if student is marked absent
                            if (att.Status == "Absent")
                            {
                                absentStudents.Add((att.StudentId, request.ClassId, selectedDate));
                            }
                        }
                    }
                    else
                    {
                        // Create new attendance record
                        currentAttendanceCount++;
                        var attId = $"ATT{(currentAttendanceCount + 1):D5}";

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
                        
                        // Track if student is marked absent
                        if (att.Status == "Absent")
                        {
                            absentStudents.Add((att.StudentId, request.ClassId, selectedDate));
                        }
                    }
                }

                // Save all changes at once
                await _context.SaveChangesAsync();

                // Send notifications for absent students
                foreach (var (studentId, classId, date) in absentStudents)
                {
                    await SendAbsenceNotification(studentId, classId, date);
                }

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

                // Prevent changing attendance if student is on Leave
                if (existing != null && existing.Status == "Leave")
                {
                    return Json(new { success = false, message = "Cannot change attendance for student on leave" });
                }

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
                
                // Send notifications for absent attendance and check low attendance
                if (request.Status == "Absent")
                {
                    await SendAttendanceNotifications(request.StudentId, request.ClassId, selectedDate, "Absent");
                }

                // Send notification if student is marked absent
                if (request.Status == "Absent")
                {
                    await SendAbsenceNotification(request.StudentId, request.ClassId, selectedDate);
                }

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
        
        // Helper method to send attendance notifications
        private async Task SendAttendanceNotifications(string studentId, string classId, DateTime date, string status)
        {
            try
            {
                var student = await _context.Students
                    .Include(s => s.User)
                    .Include(s => s.Parent)
                    .ThenInclude(p => p.User)
                    .FirstOrDefaultAsync(s => s.StudentId == studentId);
                
                var classEntity = await _context.Classes.FirstOrDefaultAsync(c => c.ClassId == classId);
                
                if (student == null || classEntity == null) return;
                
                // Notify student about absent attendance
                var studentNotificationId = IdGenerator.GenerateNotificationId(_context);
                var studentNotification = new Notification
                {
                    NotificationId = studentNotificationId,
                    UserId = student.UserId,
                    Type = "Attendance Marked",
                    Description = $"You were marked {status} for {classEntity.ClassName} on {date:dd MMM yyyy}",
                    Status = "unread",
                    CreatedDate = DateTime.Now
                };
                _context.Notifications.Add(studentNotification);
                
                // Notify parent if exists
                if (student.Parent?.User != null)
                {
                    var parentNotificationId = IdGenerator.GenerateNotificationId(_context);
                    var parentNotification = new Notification
                    {
                        NotificationId = parentNotificationId,
                        UserId = student.Parent.UserId,
                        Type = "Child Attendance Alert",
                        Description = $"Your child {student.User.FullName} was marked {status} for {classEntity.ClassName} on {date:dd MMM yyyy}",
                        Status = "unread",
                        CreatedDate = DateTime.Now
                    };
                    _context.Notifications.Add(parentNotification);
                }
                
                // Check for low attendance (below 60%)
                var totalClasses = await _context.Attendances
                    .Where(a => a.StudentId == studentId && a.ClassId == classId)
                    .CountAsync();
                
                if (totalClasses >= 5) // Only check if at least 5 classes
                {
                    var presentCount = await _context.Attendances
                        .Where(a => a.StudentId == studentId && a.ClassId == classId && 
                               (a.Status == "Present" || a.Status == "Late"))
                        .CountAsync();
                    
                    var attendanceRate = (double)presentCount / totalClasses * 100;
                    
                    if (attendanceRate < 60)
                    {
                        // Notify admins
                        var adminUsers = await _context.Users.Where(u => u.UserType == "Admin").ToListAsync();
                        foreach (var admin in adminUsers)
                        {
                            var adminNotificationId = IdGenerator.GenerateNotificationId(_context);
                            var adminNotification = new Notification
                            {
                                NotificationId = adminNotificationId,
                                UserId = admin.UserId,
                                Type = "Low Attendance Alert",
                                Description = $"Student {student.User.FullName} has low attendance in {classEntity.ClassName}: {attendanceRate:F1}% ({presentCount}/{totalClasses})",
                                Status = "unread",
                                CreatedDate = DateTime.Now
                            };
                            _context.Notifications.Add(adminNotification);
                        }
                        
                        // Notify parent
                        if (student.Parent?.User != null)
                        {
                            var parentLowAttendanceId = IdGenerator.GenerateNotificationId(_context);
                            var parentLowAttendance = new Notification
                            {
                                NotificationId = parentLowAttendanceId,
                                UserId = student.Parent.UserId,
                                Type = "Low Attendance Warning",
                                Description = $"Warning: Your child {student.User.FullName} has low attendance in {classEntity.ClassName}: {attendanceRate:F1}% ({presentCount}/{totalClasses} classes attended)",
                                Status = "unread",
                                CreatedDate = DateTime.Now
                            };
                            _context.Notifications.Add(parentLowAttendance);
                        }
                    }
                }
                
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending attendance notifications: {ex.Message}");
            }
        }


        // View Attendance Records with filters
        public async Task<IActionResult> AttendanceRecords(string? studentId, string? classId, DateTime? startDate, DateTime? endDate)
        {
            ViewBag.ActiveMenu = "AttendanceManagement";
            ViewBag.ActiveSubmenu = "Records";
            ViewBag.Title = _localization["AttendanceRecords"];
            ViewBag.Localization = _localization;

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
            var leaveCount = records.Count(a => a.Status == "Leave");
            var attendanceRate = totalRecords > 0 ? Math.Round((decimal)presentCount / totalRecords * 100, 1) : 0;

            ViewBag.TotalRecords = totalRecords;
            ViewBag.PresentCount = presentCount;
            ViewBag.AbsentCount = absentCount;
            ViewBag.LeaveCount = leaveCount;
            ViewBag.AttendanceRate = attendanceRate;
            ViewBag.Students = await _context.Students.Include(s => s.User).ToListAsync();
            ViewBag.Classes = await _context.Classes.ToListAsync();

            return View(records);
        }

        // Reports & Analytics
        public async Task<IActionResult> Reports()
        {
            ViewBag.ActiveMenu = "Reports";
            ViewBag.Title = _localization["ReportsAndAnalytics"];
            ViewBag.Localization = _localization;

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
            var thisMonthLeave = thisMonthAttendances.Count(a => a.Status == "Leave");
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
                    leave = weekAttendances.Count(a => a.Status == "Leave")
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
            ViewBag.ThisMonthLeave = thisMonthLeave;
            ViewBag.ThisMonthRate = thisMonthRate;

            return View();
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ExportReportToExcel()
        {
            try
            {
                // Gather data
                var totalStudents = await _context.Students.CountAsync();
                var totalTeachers = await _context.Teachers.CountAsync();
                var totalClasses = await _context.Classes.CountAsync();
                var totalAttendance = await _context.Attendances.CountAsync();
                
                // This Month Attendance
                var startOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);
                var thisMonthAttendances = await _context.Attendances
                    .Where(a => a.Date >= startOfMonth && a.Date <= endOfMonth)
                    .ToListAsync();
                
                var presentCount = thisMonthAttendances.Count(a => a.Status == "Present");
                var absentCount = thisMonthAttendances.Count(a => a.Status == "Absent");
                var leaveCount = thisMonthAttendances.Count(a => a.Status == "Leave");
                var totalRecords = thisMonthAttendances.Count;
                var attendanceRate = totalRecords > 0 
                    ? Math.Round((decimal)presentCount / totalRecords * 100, 1) 
                    : 0;
                
                // Top Students
                var topStudentsData = await _context.Students
                    .Include(s => s.User)
                    .Include(s => s.Attendances)
                    .Where(s => s.Attendances.Any())
                    .ToListAsync();
                
                var topStudents = topStudentsData
                    .Select(s => new
                    {
                        s.StudentId,
                        FullName = s.User.FullName,
                        Total = s.Attendances.Count,
                        Present = s.Attendances.Count(a => a.Status == "Present"),
                        Rate = s.Attendances.Count > 0 
                            ? Math.Round((decimal)s.Attendances.Count(a => a.Status == "Present") / s.Attendances.Count * 100, 1) 
                            : 0
                    })
                    .OrderByDescending(s => s.Rate)
                    .ThenByDescending(s => s.Total)
                    .Take(10)
                    .ToList();
                
                // Class Performance
                var classesData = await _context.Classes
                    .Include(c => c.Enrollments)
                    .Include(c => c.Teacher).ThenInclude(t => t.User)
                    .Include(c => c.Subject)
                    .ToListAsync();
                
                var classes = classesData.Select(cls => new
                {
                    cls.ClassId,
                    cls.ClassName,
                    TeacherName = cls.Teacher?.User?.FullName ?? "N/A",
                    SubjectName = cls.Subject?.SubjectName ?? "N/A",
                    cls.CurrentCapacity,
                    cls.MaxCapacity,
                    FillRate = cls.MaxCapacity > 0 
                        ? Math.Round((decimal)cls.CurrentCapacity / cls.MaxCapacity * 100, 1) 
                        : 0,
                    Status = cls.CurrentCapacity >= cls.MaxCapacity ? "Full" :
                            cls.CurrentCapacity >= (cls.MaxCapacity * 0.9) ? "Almost Full" : "Active"
                }).OrderByDescending(c => c.CurrentCapacity).ToList();
                
                // Generate PDF
                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(50);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Black));
                        
                        page.Header().Row(row =>
                        {
                            row.RelativeItem().Column(column =>
                            {
                                column.Item().Text("ATTENDANCE MANAGEMENT SYSTEM")
                                    .FontSize(20).Bold().FontColor(Colors.Blue.Darken2);
                                column.Item().Text("Comprehensive Report")
                                    .FontSize(14).FontColor(Colors.Grey.Darken1);
                                column.Item().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
                                    .FontSize(9).FontColor(Colors.Grey.Medium);
                            });
                        });
                        
                        page.Content().PaddingVertical(20).Column(column =>
                        {
                            // System Overview Section
                            column.Item().Text("SYSTEM OVERVIEW").FontSize(14).Bold().FontColor(Colors.Blue.Medium);
                            column.Item().PaddingVertical(5);
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(1);
                                });
                                
                                table.Header(header =>
                                {
                                    header.Cell().Background(Colors.Blue.Lighten3).Padding(5)
                                        .Text("Metric").Bold();
                                    header.Cell().Background(Colors.Blue.Lighten3).Padding(5)
                                        .AlignRight().Text("Count").Bold();
                                });
                                
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text("Total Students");
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text(totalStudents.ToString());
                                
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text("Total Teachers");
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text(totalTeachers.ToString());
                                
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text("Total Classes");
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text(totalClasses.ToString());
                                
                                table.Cell().Padding(5).Text("Total Attendance Records");
                                table.Cell().Padding(5).AlignRight().Text(totalAttendance.ToString());
                            });
                            
                            column.Item().PaddingVertical(15);
                            
                            // Attendance Summary
                            column.Item().Text($"ATTENDANCE SUMMARY - {DateTime.Now:MMMM yyyy}")
                                .FontSize(14).Bold().FontColor(Colors.Blue.Medium);
                            column.Item().PaddingVertical(5);
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(1);
                                });
                                
                                table.Header(header =>
                                {
                                    header.Cell().Background(Colors.Green.Lighten3).Padding(5)
                                        .Text("Status").Bold();
                                    header.Cell().Background(Colors.Green.Lighten3).Padding(5)
                                        .AlignRight().Text("Count").Bold();
                                });
                                
                                table.Cell().Background(Colors.Green.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                    .Padding(5).Text("Present");
                                table.Cell().Background(Colors.Green.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                    .Padding(5).AlignRight().Text(presentCount.ToString());
                                
                                table.Cell().Background(Colors.Red.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                    .Padding(5).Text("Absent");
                                table.Cell().Background(Colors.Red.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                    .Padding(5).AlignRight().Text(absentCount.ToString());
                                
                                table.Cell().Background(Colors.Yellow.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                    .Padding(5).Text("Leave");
                                table.Cell().Background(Colors.Yellow.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                    .Padding(5).AlignRight().Text(leaveCount.ToString());
                                
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text("Total Records");
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text(totalRecords.ToString());
                                
                                table.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Attendance Rate").Bold();
                                table.Cell().Background(Colors.Blue.Lighten4).Padding(5).AlignRight().Text($"{attendanceRate}%").Bold();
                            });
                            
                            column.Item().PaddingVertical(15);
                            column.Item().PageBreak();
                            
                            // Top Students
                            column.Item().Text("TOP PERFORMING STUDENTS (BY ATTENDANCE)")
                                .FontSize(14).Bold().FontColor(Colors.Blue.Medium);
                            column.Item().PaddingVertical(5);
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                });
                                
                                table.Header(header =>
                                {
                                    header.Cell().Background(Colors.Orange.Lighten3).Padding(5).Text("Student ID").Bold();
                                    header.Cell().Background(Colors.Orange.Lighten3).Padding(5).Text("Name").Bold();
                                    header.Cell().Background(Colors.Orange.Lighten3).Padding(5).AlignRight().Text("Total").Bold();
                                    header.Cell().Background(Colors.Orange.Lighten3).Padding(5).AlignRight().Text("Present").Bold();
                                    header.Cell().Background(Colors.Orange.Lighten3).Padding(5).AlignRight().Text("Rate %").Bold();
                                });
                                
                                foreach (var student in topStudents)
                                {
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(student.StudentId);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(student.FullName);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text(student.Total.ToString());
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text(student.Present.ToString());
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text($"{student.Rate}%");
                                }
                            });
                            
                            column.Item().PaddingVertical(15);
                            
                            // Class Enrollment Summary
                            column.Item().Text("CLASS ENROLLMENT SUMMARY")
                                .FontSize(14).Bold().FontColor(Colors.Blue.Medium);
                            column.Item().PaddingVertical(5);
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                });
                                
                                table.Header(header =>
                                {
                                    header.Cell().Background(Colors.Purple.Lighten3).Padding(3).Text("ID").FontSize(8).Bold();
                                    header.Cell().Background(Colors.Purple.Lighten3).Padding(3).Text("Class").FontSize(8).Bold();
                                    header.Cell().Background(Colors.Purple.Lighten3).Padding(3).Text("Teacher").FontSize(8).Bold();
                                    header.Cell().Background(Colors.Purple.Lighten3).Padding(3).Text("Subject").FontSize(8).Bold();
                                    header.Cell().Background(Colors.Purple.Lighten3).Padding(3).AlignRight().Text("Current").FontSize(8).Bold();
                                    header.Cell().Background(Colors.Purple.Lighten3).Padding(3).AlignRight().Text("Max").FontSize(8).Bold();
                                    header.Cell().Background(Colors.Purple.Lighten3).Padding(3).AlignRight().Text("Fill%").FontSize(8).Bold();
                                    header.Cell().Background(Colors.Purple.Lighten3).Padding(3).Text("Status").FontSize(8).Bold();
                                });
                                
                                foreach (var cls in classes)
                                {
                                    var bgColor = cls.Status == "Full" ? Colors.Red.Lighten4 :
                                                 cls.Status == "Almost Full" ? Colors.Orange.Lighten4 : Colors.White;
                                    
                                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(cls.ClassId).FontSize(8);
                                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(cls.ClassName).FontSize(8);
                                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(cls.TeacherName).FontSize(8);
                                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(cls.SubjectName).FontSize(8);
                                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(cls.CurrentCapacity.ToString()).FontSize(8);
                                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(cls.MaxCapacity.ToString()).FontSize(8);
                                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text($"{cls.FillRate}%").FontSize(8);
                                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(cls.Status).FontSize(8);
                                }
                            });
                        });
                        
                        page.Footer().AlignCenter().Text(text =>
                        {
                            text.Span("Page ");
                            text.CurrentPageNumber();
                            text.Span(" of ");
                            text.TotalPages();
                        });
                    });
                });
                
                var pdfBytes = document.GeneratePdf();
                return File(pdfBytes, "application/pdf", $"Comprehensive_Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error generating report: {ex.Message}";
                return RedirectToAction(nameof(Reports));
            }
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ExportStudentReport()
        {
            try
            {
                var studentsData = await _context.Students
                    .Include(s => s.User)
                    .Include(s => s.Enrollments)
                    .Include(s => s.Attendances)
                    .OrderBy(s => s.StudentId)
                    .ToListAsync();
                
                var students = studentsData.Select(student => new
                {
                    student.StudentId,
                    FullName = student.User.FullName,
                    Email = student.User.Email,
                    Phone = student.User.PhoneNumber ?? "N/A",
                    student.Gender,
                    Status = student.User.Status,
                    EnrollmentDate = student.EnrollmentDate?.ToString("yyyy-MM-dd") ?? "N/A",
                    TotalClasses = student.Enrollments?.Count(e => e.UnenrolledDate == null) ?? 0,
                    TotalAttendance = student.Attendances?.Count ?? 0,
                    Present = student.Attendances?.Count(a => a.Status == "Present") ?? 0,
                    Absent = student.Attendances?.Count(a => a.Status == "Absent") ?? 0,
                    Leave = student.Attendances?.Count(a => a.Status == "Leave") ?? 0,
                    Rate = (student.Attendances?.Count ?? 0) > 0 
                        ? Math.Round((decimal)(student.Attendances?.Count(a => a.Status == "Present") ?? 0) / student.Attendances.Count * 100, 1) 
                        : 0
                }).ToList();
                
                // Generate PDF
                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.Margin(40);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Black));
                        
                        page.Header().Row(row =>
                        {
                            row.RelativeItem().Column(column =>
                            {
                                column.Item().Text("STUDENT ATTENDANCE DETAILED REPORT")
                                    .FontSize(18).Bold().FontColor(Colors.Blue.Darken2);
                                column.Item().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
                                    .FontSize(9).FontColor(Colors.Grey.Medium);
                                column.Item().Text($"Total Students: {students.Count}")
                                    .FontSize(10).Bold().FontColor(Colors.Blue.Medium);
                            });
                        });
                        
                        page.Content().PaddingVertical(15).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(0.8f);
                                columns.RelativeColumn(0.8f);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(0.8f);
                                columns.RelativeColumn(0.8f);
                                columns.RelativeColumn(0.8f);
                                columns.RelativeColumn(0.8f);
                                columns.RelativeColumn(0.8f);
                                columns.RelativeColumn(1);
                            });
                            
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Blue.Medium).Padding(4).Text("Student ID").FontSize(8).Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Blue.Medium).Padding(4).Text("Full Name").FontSize(8).Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Blue.Medium).Padding(4).Text("Email").FontSize(8).Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Blue.Medium).Padding(4).Text("Phone").FontSize(8).Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Blue.Medium).Padding(4).Text("Gender").FontSize(8).Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Blue.Medium).Padding(4).Text("Status").FontSize(8).Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Blue.Medium).Padding(4).Text("Enrollment").FontSize(8).Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Blue.Medium).Padding(4).AlignRight().Text("Classes").FontSize(8).Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Blue.Medium).Padding(4).AlignRight().Text("Total").FontSize(8).Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Blue.Medium).Padding(4).AlignRight().Text("Present").FontSize(8).Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Blue.Medium).Padding(4).AlignRight().Text("Absent").FontSize(8).Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Blue.Medium).Padding(4).AlignRight().Text("Leave").FontSize(8).Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Blue.Medium).Padding(4).AlignRight().Text("Rate %").FontSize(8).Bold().FontColor(Colors.White);
                            });
                            
                            bool isAlternate = false;
                            foreach (var student in students)
                            {
                                var bgColor = isAlternate ? Colors.Grey.Lighten4 : Colors.White;
                                var statusColor = student.Status == "Active" ? Colors.Green.Lighten4 : Colors.Red.Lighten4;
                                var rateColor = student.Rate >= 80 ? Colors.Green.Lighten4 :
                                               student.Rate >= 60 ? Colors.Orange.Lighten4 : Colors.Red.Lighten4;
                                
                                table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(student.StudentId).FontSize(7);
                                table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(student.FullName).FontSize(7);
                                table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(student.Email).FontSize(7);
                                table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(student.Phone).FontSize(7);
                                table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(student.Gender).FontSize(7);
                                table.Cell().Background(statusColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(student.Status).FontSize(7);
                                table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(student.EnrollmentDate).FontSize(7);
                                table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignRight().Text(student.TotalClasses.ToString()).FontSize(7);
                                table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignRight().Text(student.TotalAttendance.ToString()).FontSize(7);
                                table.Cell().Background(Colors.Green.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignRight().Text(student.Present.ToString()).FontSize(7);
                                table.Cell().Background(Colors.Red.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignRight().Text(student.Absent.ToString()).FontSize(7);
                                table.Cell().Background(Colors.Yellow.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignRight().Text(student.Leave.ToString()).FontSize(7);
                                table.Cell().Background(rateColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignRight().Text($"{student.Rate}%").FontSize(7).Bold();
                                
                                isAlternate = !isAlternate;
                            }
                        });
                        
                        page.Footer().AlignCenter().Text(text =>
                        {
                            text.Span("Page ");
                            text.CurrentPageNumber();
                            text.Span(" of ");
                            text.TotalPages();
                        });
                    });
                });
                
                var pdfBytes = document.GeneratePdf();
                return File(pdfBytes, "application/pdf", $"Student_Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error generating student report: {ex.Message}";
                return RedirectToAction(nameof(Reports));
            }
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ExportClassReport()
        {
            try
            {
                var classesData = await _context.Classes
                    .Include(c => c.Teacher).ThenInclude(t => t.User)
                    .Include(c => c.Subject)
                    .Include(c => c.Enrollments)
                    .OrderBy(c => c.ClassId)
                    .ToListAsync();
                
                // Create a typed list for class data
                var classes = new List<(string ClassId, string ClassName, string SubjectName, string TeacherName, 
                    string Room, string Schedule, int CurrentCapacity, int MaxCapacity, decimal FillRate, 
                    int AttendanceCount, string Status)>();
                    
                foreach (var cls in classesData)
                {
                    var fillRate = cls.MaxCapacity > 0 
                        ? Math.Round((decimal)cls.CurrentCapacity / cls.MaxCapacity * 100, 1) 
                        : 0;
                    
                    var attendanceCount = await _context.Attendances.CountAsync(a => a.ClassId == cls.ClassId);
                    var schedule = $"{cls.Day} {cls.StartTime?.ToString(@"hh\:mm")}-{cls.EndTime?.ToString(@"hh\:mm")}";
                    
                    classes.Add((
                        cls.ClassId,
                        cls.ClassName,
                        cls.Subject?.SubjectName ?? "N/A",
                        cls.Teacher?.User?.FullName ?? "N/A",
                        cls.RoomNumber ?? "N/A",
                        schedule,
                        cls.CurrentCapacity,
                        cls.MaxCapacity,
                        fillRate,
                        attendanceCount,
                        cls.IsActive ? "Active" : "Inactive"
                    ));
                }
                
                // Generate PDF
                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.Margin(40);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Black));
                        
                        page.Header().Row(row =>
                        {
                            row.RelativeItem().Column(column =>
                            {
                                column.Item().Text("CLASS PERFORMANCE DETAILED REPORT")
                                    .FontSize(18).Bold().FontColor(Colors.Blue.Darken2);
                                column.Item().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
                                    .FontSize(9).FontColor(Colors.Grey.Medium);
                                column.Item().Text($"Total Classes: {classes.Count}")
                                    .FontSize(10).Bold().FontColor(Colors.Blue.Medium);
                            });
                        });
                        
                        page.Content().PaddingVertical(15).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(2.5f);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                            });
                            
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Purple.Medium).Padding(4).Text("Class ID").FontSize(8).Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Purple.Medium).Padding(4).Text("Class Name").FontSize(8).Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Purple.Medium).Padding(4).Text("Subject").FontSize(8).Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Purple.Medium).Padding(4).Text("Teacher").FontSize(8).Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Purple.Medium).Padding(4).Text("Room").FontSize(8).Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Purple.Medium).Padding(4).Text("Schedule").FontSize(8).Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Purple.Medium).Padding(4).AlignRight().Text("Current").FontSize(8).Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Purple.Medium).Padding(4).AlignRight().Text("Max").FontSize(8).Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Purple.Medium).Padding(4).AlignRight().Text("Fill%").FontSize(8).Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Purple.Medium).Padding(4).AlignRight().Text("Attend").FontSize(8).Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Purple.Medium).Padding(4).Text("Status").FontSize(8).Bold().FontColor(Colors.White);
                            });
                            
                            bool isAlternate = false;
                            foreach (var cls in classes)
                            {
                                var bgColor = isAlternate ? Colors.Grey.Lighten4 : Colors.White;
                                var fillColor = cls.FillRate >= 100 ? Colors.Red.Lighten4 :
                                               cls.FillRate >= 90 ? Colors.Orange.Lighten4 :
                                               cls.FillRate >= 70 ? Colors.Yellow.Lighten4 : Colors.Green.Lighten4;
                                var statusColor = cls.Status == "Active" ? Colors.Green.Lighten4 : Colors.Grey.Lighten3;
                                
                                table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(cls.ClassId).FontSize(7);
                                table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(cls.ClassName).FontSize(7);
                                table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(cls.SubjectName).FontSize(7);
                                table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(cls.TeacherName).FontSize(7);
                                table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(cls.Room).FontSize(7);
                                table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(cls.Schedule).FontSize(7);
                                table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignRight().Text(cls.CurrentCapacity.ToString()).FontSize(7);
                                table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignRight().Text(cls.MaxCapacity.ToString()).FontSize(7);
                                table.Cell().Background(fillColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignRight().Text($"{cls.FillRate}%").FontSize(7).Bold();
                                table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignRight().Text(cls.AttendanceCount.ToString()).FontSize(7);
                                table.Cell().Background(statusColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(cls.Status).FontSize(7);
                                
                                isAlternate = !isAlternate;
                            }
                        });
                        
                        page.Footer().AlignCenter().Text(text =>
                        {
                            text.Span("Page ");
                            text.CurrentPageNumber();
                            text.Span(" of ");
                            text.TotalPages();
                        });
                    });
                });
                
                var pdfBytes = document.GeneratePdf();
                return File(pdfBytes, "application/pdf", $"Class_Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error generating class report: {ex.Message}";
                return RedirectToAction(nameof(Reports));
            }
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ExportAttendanceReport(DateTime? startDate, DateTime? endDate)
        {
            try
            {
                var start = startDate ?? DateTime.Now.AddMonths(-1);
                var end = endDate ?? DateTime.Now;
                
                var attendancesData = await _context.Attendances
                    .Include(a => a.Student).ThenInclude(s => s.User)
                    .Include(a => a.Class)
                    .Where(a => a.Date >= start && a.Date <= end)
                    .OrderBy(a => a.Date)
                    .ThenBy(a => a.ClassId)
                    .ThenBy(a => a.StudentId)
                    .ToListAsync();
                
                var attendances = attendancesData.Select(att => new
                {
                    att.AttendanceId,
                    Date = att.Date.ToString("yyyy-MM-dd"),
                    StudentId = att.Student?.StudentId ?? "N/A",
                    StudentName = att.Student?.User?.FullName ?? "N/A",
                    ClassId = att.Class?.ClassId ?? "N/A",
                    ClassName = att.Class?.ClassName ?? "N/A",
                    att.Status,
                    TakenOn = att.TakenOn.ToString("yyyy-MM-dd HH:mm"),
                    MarkedBy = att.MarkedByTeacherId ?? "System"
                }).ToList();
                
                // Calculate statistics
                var totalRecords = attendances.Count;
                var presentCount = attendances.Count(a => a.Status == "Present");
                var absentCount = attendances.Count(a => a.Status == "Absent");
                var leaveCount = attendances.Count(a => a.Status == "Leave");
                var attendanceRate = totalRecords > 0 
                    ? Math.Round((decimal)presentCount / totalRecords * 100, 1) 
                    : 0;
                
                // Generate PDF
                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.Margin(40);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Black));
                        
                        page.Header().Row(row =>
                        {
                            row.RelativeItem().Column(column =>
                            {
                                column.Item().Text("ATTENDANCE RECORDS DETAILED REPORT")
                                    .FontSize(18).Bold().FontColor(Colors.Blue.Darken2);
                                column.Item().Text($"Period: {start:yyyy-MM-dd} to {end:yyyy-MM-dd}")
                                    .FontSize(11).FontColor(Colors.Blue.Medium);
                                column.Item().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
                                    .FontSize(9).FontColor(Colors.Grey.Medium);
                            });
                            
                            row.ConstantItem(200).Column(column =>
                            {
                                column.Item().AlignRight().Text("Summary Statistics").FontSize(10).Bold();
                                column.Item().AlignRight().Text($"Total Records: {totalRecords}").FontSize(9);
                                column.Item().AlignRight().Text($"Present: {presentCount}").FontSize(9).FontColor(Colors.Green.Darken1);
                                column.Item().AlignRight().Text($"Absent: {absentCount}").FontSize(9).FontColor(Colors.Red.Darken1);
                                column.Item().AlignRight().Text($"Leave: {leaveCount}").FontSize(9).FontColor(Colors.Orange.Darken1);
                                column.Item().AlignRight().Text($"Rate: {attendanceRate}%").FontSize(9).Bold();
                            });
                        });
                        
                        page.Content().PaddingVertical(15).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.2f);
                            });
                            
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Teal.Medium).Padding(4).Text("Attendance ID").FontSize(8).Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Teal.Medium).Padding(4).Text("Date").FontSize(8).Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Teal.Medium).Padding(4).Text("Student ID").FontSize(8).Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Teal.Medium).Padding(4).Text("Student Name").FontSize(8).Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Teal.Medium).Padding(4).Text("Class ID").FontSize(8).Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Teal.Medium).Padding(4).Text("Class Name").FontSize(8).Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Teal.Medium).Padding(4).Text("Status").FontSize(8).Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Teal.Medium).Padding(4).Text("Taken On").FontSize(8).Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Teal.Medium).Padding(4).Text("Marked By").FontSize(8).Bold().FontColor(Colors.White);
                            });
                            
                            bool isAlternate = false;
                            foreach (var att in attendances)
                            {
                                var bgColor = isAlternate ? Colors.Grey.Lighten4 : Colors.White;
                                var statusColor = att.Status == "Present" ? Colors.Green.Lighten4 :
                                                 att.Status == "Absent" ? Colors.Red.Lighten4 : Colors.Yellow.Lighten4;
                                
                                table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(att.AttendanceId).FontSize(7);
                                table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(att.Date).FontSize(7);
                                table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(att.StudentId).FontSize(7);
                                table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(att.StudentName).FontSize(7);
                                table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(att.ClassId).FontSize(7);
                                table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(att.ClassName).FontSize(7);
                                table.Cell().Background(statusColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(att.Status).FontSize(7).Bold();
                                table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(att.TakenOn).FontSize(7);
                                table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(att.MarkedBy).FontSize(7);
                                
                                isAlternate = !isAlternate;
                            }
                        });
                        
                        page.Footer().AlignCenter().Text(text =>
                        {
                            text.Span("Page ");
                            text.CurrentPageNumber();
                            text.Span(" of ");
                            text.TotalPages();
                        });
                    });
                });
                
                var pdfBytes = document.GeneratePdf();
                return File(pdfBytes, "application/pdf", $"Attendance_Records_{start:yyyyMMdd}_to_{end:yyyyMMdd}.pdf");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error generating attendance records report: {ex.Message}";
                return RedirectToAction(nameof(Reports));
            }
        }

        // ==================== LEAVE MANAGEMENT ====================

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> LeaveIndex(string? status, string? search, DateTime? startDate, DateTime? endDate)
        {
            ViewBag.ActiveMenu = "LeaveManagement";
            ViewBag.Title = _localization["LeaveManagement"];
            ViewBag.Localization = _localization;

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
            ViewBag.Title = _localization["LeaveApplicationDetails"];
            ViewBag.Localization = _localization;

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
                                    var attendanceId = IdGenerator.GenerateAttendanceId(_context);
                                    var attendance = new Attendance
                                    {
                                        AttendanceId = attendanceId,
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
                var notificationId = IdGenerator.GenerateNotificationId(_context);
                var notification = new Notification
                {
                    NotificationId = notificationId,
                    UserId = leave.UserId,
                    Type = "Leave Approved",
                    Description = $"Your leave application from {leave.StartDate:dd MMM yyyy} to {leave.EndDate:dd MMM yyyy} has been approved." + 
                                  (string.IsNullOrEmpty(remarks) ? "" : $" Remarks: {remarks}"),
                    RelatedEntityId = leave.LeaveId,
                    Status = "unread",
                    CreatedDate = DateTime.Now
                };
                _context.Notifications.Add(notification);
                
                // Notify parent if student has one
                var studentForParentNotif = await _context.Students
                    .Include(s => s.Parent)
                    .ThenInclude(p => p.User)
                    .FirstOrDefaultAsync(s => s.UserId == leave.UserId);
                
                if (studentForParentNotif?.Parent?.User != null)
                {
                    var parentNotificationId = IdGenerator.GenerateNotificationId(_context);
                    var parentNotification = new Notification
                    {
                        NotificationId = parentNotificationId,
                        UserId = studentForParentNotif.Parent.UserId,
                        Type = "Child Leave Approved",
                        Description = $"Leave application for your child {leave.User.FullName} from {leave.StartDate:dd MMM yyyy} to {leave.EndDate:dd MMM yyyy} has been approved.",
                        RelatedEntityId = leave.LeaveId,
                        Status = "unread",
                        CreatedDate = DateTime.Now
                    };
                    _context.Notifications.Add(parentNotification);
                }

                // Delete only the admin leave application notifications
                var adminLeaveNotifications = await _context.Notifications
                    .Where(n => n.RelatedEntityId == leave.LeaveId && n.Type == "Leave Application")
                    .ToListAsync();
                
                if (adminLeaveNotifications.Any())
                {
                    _context.Notifications.RemoveRange(adminLeaveNotifications);
                }

                await _context.SaveChangesAsync();

                // Send email to student
                try
                {
                    _helper.SendLeaveApprovalEmail(
                        leave.User.Email,
                        leave.User.FullName,
                        leave.StartDate,
                        leave.EndDate,
                        leave.TotalDays,
                        leave.Reason,
                        remarks
                    );
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
                var notificationId = IdGenerator.GenerateNotificationId(_context);
                var notification = new Notification
                {
                    NotificationId = notificationId,
                    UserId = leave.UserId,
                    Type = "Leave Rejected",
                    Description = $"Your leave application from {leave.StartDate:dd MMM yyyy} to {leave.EndDate:dd MMM yyyy} has been rejected." + 
                                  (string.IsNullOrEmpty(remarks) ? "" : $" Reason: {remarks}"),
                    RelatedEntityId = leave.LeaveId,
                    Status = "unread",
                    CreatedDate = DateTime.Now
                };
                _context.Notifications.Add(notification);
                
                // Notify parent if student has one
                var student = await _context.Students
                    .Include(s => s.Parent)
                    .ThenInclude(p => p.User)
                    .FirstOrDefaultAsync(s => s.UserId == leave.UserId);
                
                if (student?.Parent?.User != null)
                {
                    var parentNotificationId = IdGenerator.GenerateNotificationId(_context);
                    var parentNotification = new Notification
                    {
                        NotificationId = parentNotificationId,
                        UserId = student.Parent.UserId,
                        Type = "Child Leave Rejected",
                        Description = $"Leave application for your child {leave.User.FullName} from {leave.StartDate:dd MMM yyyy} to {leave.EndDate:dd MMM yyyy} has been rejected." +
                                      (string.IsNullOrEmpty(remarks) ? "" : $" Reason: {remarks}"),
                        RelatedEntityId = leave.LeaveId,
                        Status = "unread",
                        CreatedDate = DateTime.Now
                    };
                    _context.Notifications.Add(parentNotification);
                }

                // Delete only the admin leave application notifications
                var adminLeaveNotifications = await _context.Notifications
                    .Where(n => n.RelatedEntityId == leave.LeaveId && n.Type == "Leave Application")
                    .ToListAsync();
                
                if (adminLeaveNotifications.Any())
                {
                    _context.Notifications.RemoveRange(adminLeaveNotifications);
                }

                await _context.SaveChangesAsync();

                // Send email notification
                try
                {
                    _helper.SendLeaveRejectionEmail(
                        leave.User.Email,
                        leave.User.FullName,
                        leave.StartDate,
                        leave.EndDate,
                        leave.TotalDays,
                        leave.Reason,
                        remarks
                    );
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

        // ==================== LANGUAGE SWITCHER ====================
        
        [HttpPost]
        public IActionResult ChangeLanguage(string language, string returnUrl)
        {
            _localization.SetLanguage(language);
            
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        public async Task<IActionResult> AutoTranslate(string targetLang)
        {
            var success = await _localization.AutoTranslate(targetLang);
            if (success)
            {
                return Json(new { success = true, message = $"Translations for '{targetLang}' generated successfully!" });
            }
            return Json(new { success = false, message = "Failed to generate translations." });
        }

        // ==================== SETTINGS ====================

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Settings()
        {
            ViewBag.ActiveMenu = "Settings";
            ViewBag.ActiveSubmenu = "Settings";
            ViewBag.Title = _localization["AdminSettings"];
            ViewBag.Localization = _localization;

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
            public required string UserId { get; set; }
        }

        // ==================== NOTIFICATIONS ====================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Notifications()
        {
            ViewBag.ActiveMenu = "Notifications";
            ViewBag.Title = _localization["Notifications"];
            ViewBag.Localization = _localization;

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
                (n.Type == "Leave Application" || n.Type == "Student Leave Application") &&
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

        // Delete notification
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteNotification(string notificationId)
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
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAllRead()
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
                    .Where(n => n.UserId == admin.UserId && n.Status == "read")
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

        // ==================== ANNOUNCEMENT ====================
        public async Task<IActionResult> CreateAnnouncement()
        {
            ViewBag.ActiveMenu = "Announcement";
            ViewBag.Title = _localization["CreateAnnouncement"];
            ViewBag.Localization = _localization;

            // Get all users grouped by type
            var users = await _context.Users
                .Where(u => u.UserType != "Admin")
                .OrderBy(u => u.UserType)
                .ThenBy(u => u.FullName)
                .ToListAsync();

            ViewBag.Users = users;
            ViewBag.UserTypes = users.Select(u => u.UserType).Distinct().OrderBy(t => t).ToList();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAnnouncement(
            List<string>? recipientTypes,
            List<string>? specificUserIds,
            string description)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(description))
            {
                ModelState.AddModelError("description", "Description is required");
            }

            if (recipientTypes == null || !recipientTypes.Any())
            {
                ModelState.AddModelError("recipientTypes", "Please select at least one recipient");
            }

            if (recipientTypes != null && recipientTypes.Contains("specific") && (specificUserIds == null || !specificUserIds.Any()))
            {
                ModelState.AddModelError("specificUserIds", "Please select at least one specific user");
            }

            if (!ModelState.IsValid)
            {
                // Reload data for view
                var users = await _context.Users
                    .Where(u => u.UserType != "Admin")
                    .OrderBy(u => u.UserType)
                    .ThenBy(u => u.FullName)
                    .ToListAsync();

                ViewBag.Users = users;
                ViewBag.UserTypes = users.Select(u => u.UserType).Distinct().OrderBy(t => t).ToList();
                ViewBag.ActiveMenu = "Announcement";
                ViewBag.Title = _localization["CreateAnnouncement"];
                ViewBag.Localization = _localization;

                return View();
            }

            try
            {
                // Get target users based on recipient types
                var targetUsers = new List<User>();
                var recipientDescriptions = new List<string>();

                foreach (var recipientType in recipientTypes ?? new List<string>())
                {
                    if (recipientType == "all")
                    {
                        // All users except admins
                        var allUsers = await _context.Users
                            .Where(u => u.UserType != "Admin")
                            .ToListAsync();
                        
                        foreach (var user in allUsers)
                        {
                            if (!targetUsers.Any(u => u.UserId == user.UserId))
                            {
                                targetUsers.Add(user);
                            }
                        }
                        recipientDescriptions.Add("All Users");
                    }
                    else if (recipientType == "specific")
                    {
                        // Specific users
                        if (specificUserIds != null && specificUserIds.Any())
                        {
                            var specificUsers = await _context.Users
                                .Where(u => specificUserIds.Contains(u.UserId))
                                .ToListAsync();
                            
                            foreach (var user in specificUsers)
                            {
                                if (!targetUsers.Any(u => u.UserId == user.UserId))
                                {
                                    targetUsers.Add(user);
                                }
                            }
                            recipientDescriptions.Add($"{specificUsers.Count} Specific User(s)");
                        }
                    }
                    else
                    {
                        // Single user type (Student, Teacher, Parent)
                        var typeUsers = await _context.Users
                            .Where(u => u.UserType == recipientType)
                            .ToListAsync();
                        
                        foreach (var user in typeUsers)
                        {
                            if (!targetUsers.Any(u => u.UserId == user.UserId))
                            {
                                targetUsers.Add(user);
                            }
                        }
                        recipientDescriptions.Add($"{recipientType}s");
                    }
                }

                // Create notifications for all target users
                foreach (var user in targetUsers)
                {
                    var notificationId = IdGenerator.GenerateNotificationId(_context);
                    var notification = new Notification
                    {
                        NotificationId = notificationId,
                        UserId = user.UserId,
                        Type = "Announcement",
                        Description = description,
                        Status = "unread",
                        CreatedDate = DateTime.Now
                    };
                    _context.Notifications.Add(notification);
                }

                // Also send notification to the admin who created it
                var adminEmail = User.Identity.Name;
                var admin = await _context.Users.FirstOrDefaultAsync(u => u.Email == adminEmail && u.UserType == "Admin");
                if (admin != null)
                {
                    var adminNotificationId = IdGenerator.GenerateNotificationId(_context);
                    var adminNotification = new Notification
                    {
                        NotificationId = adminNotificationId,
                        UserId = admin.UserId,
                        Type = "Announcement",
                        Description = description,
                        Status = "unread",
                        CreatedDate = DateTime.Now
                    };
                    _context.Notifications.Add(adminNotification);
                }

                await _context.SaveChangesAsync();

                var recipientDescription = string.Join(" & ", recipientDescriptions);
                TempData["SuccessMessage"] = $"Announcement sent successfully to {targetUsers.Count} user(s) ({recipientDescription})!";
                return RedirectToAction(nameof(Notifications));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error sending announcement: {ex.Message}";
                
                // Reload data for view
                var users = await _context.Users
                    .Where(u => u.UserType != "Admin")
                    .OrderBy(u => u.UserType)
                    .ThenBy(u => u.FullName)
                    .ToListAsync();

                ViewBag.Users = users;
                ViewBag.UserTypes = users.Select(u => u.UserType).Distinct().OrderBy(t => t).ToList();
                ViewBag.ActiveMenu = "Announcement";
                ViewBag.Title = _localization["CreateAnnouncement"];
                ViewBag.Localization = _localization;

                return View();
            }
        }

        // Get notification details
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetNotificationDetails(string notificationId)
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

                // Build detailed data based on notification type
                object detailData = null;

                // Handle Announcement type separately as it doesn't need RelatedEntityId
                if (notification.Type == "Announcement")
                {
                    detailData = new
                    {
                        message = notification.Description,
                        sentBy = "Administrator",
                        createdDate = notification.CreatedDate.ToString("dd MMM yyyy hh:mm tt")
                    };
                }
                else
                {
                    // Other notification types that require RelatedEntityId
                    switch (notification.Type)
                {
                    case "Class Assignment":
                        if (!string.IsNullOrEmpty(notification.RelatedEntityId))
                        {
                            var classInfo = await _context.Classes
                                .Include(c => c.Teacher)
                                    .ThenInclude(t => t.User)
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

                    case "Student Enrollment":
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
                                // Split the comma-separated student IDs
                                var affectedStudentIds = notification.AffectedEntityId?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? new string[0];
                                
                                var enrolledStudents = classInfo.Enrollments
                                    .Where(e => affectedStudentIds.Contains(e.Student.StudentId))
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

                    case "Leave Application":
                    case "Student Leave Application":
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

                    case "Leave Approved":
                    case "Leave Rejected":
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
                                    startDate = leave.StartDate.ToString("dd MMM yyyy"),
                                    endDate = leave.EndDate.ToString("dd MMM yyyy"),
                                    totalDays = leave.TotalDays,
                                    reason = leave.Reason,
                                    status = leave.Status,
                                    remarks = leave.Remarks
                                };
                            }
                        }
                        break;

                    case "Student Registration":
                        if (!string.IsNullOrEmpty(notification.RelatedEntityId))
                        {
                            var student = await _context.Students
                                .Include(s => s.User)
                                .FirstOrDefaultAsync(s => s.StudentId == notification.RelatedEntityId);

                            if (student != null)
                            {
                                List<object> enrolledClasses = new List<object>();
                                
                                // Get class details from AffectedEntityId
                                if (!string.IsNullOrEmpty(notification.AffectedEntityId))
                                {
                                    var classIds = notification.AffectedEntityId.Split(',', StringSplitOptions.RemoveEmptyEntries);
                                    var classes = await _context.Classes
                                        .Include(c => c.Teacher)
                                            .ThenInclude(t => t.User)
                                        .Include(c => c.Subject)
                                        .Where(c => classIds.Contains(c.ClassId))
                                        .ToListAsync();

                                    enrolledClasses = classes.Select(c => new
                                    {
                                        classId = c.ClassId,
                                        className = c.ClassName,
                                        teacher = c.Teacher?.User?.FullName ?? "N/A",
                                        venue = c.RoomNumber ?? "N/A",
                                        day = c.Day ?? "N/A",
                                        time = c.StartTime != null && c.EndTime != null 
                                            ? $"{c.StartTime.Value:hh\\:mm} - {c.EndTime.Value:hh\\:mm}" 
                                            : "N/A",
                                        capacity = $"{c.CurrentCapacity}/{c.MaxCapacity}",
                                        subject = c.Subject?.SubjectName ?? "N/A"
                                    }).Cast<object>().ToList();
                                }

                                detailData = new
                                {
                                    userId = student.User.UserId,
                                    fullName = student.User.FullName,
                                    email = student.User.Email,
                                    phoneNumber = student.User.PhoneNumber,
                                    userType = student.User.UserType,
                                    status = student.User.Status,
                                    createdDate = student.User.CreatedDate.ToString("dd MMM yyyy"),
                                    enrolledClassesCount = enrolledClasses.Count,
                                    enrolledClasses = enrolledClasses
                                };
                            }
                        }
                        break;

                    case "Teacher Registration":
                    case "Parent Registration":
                        if (!string.IsNullOrEmpty(notification.RelatedEntityId))
                        {
                            var user = await _context.Users
                                .FirstOrDefaultAsync(u => u.UserId == notification.RelatedEntityId);

                            if (user != null)
                            {
                                detailData = new
                                {
                                    userId = user.UserId,
                                    fullName = user.FullName,
                                    email = user.Email,
                                    phoneNumber = user.PhoneNumber,
                                    userType = user.UserType,
                                    status = user.Status,
                                    createdDate = user.CreatedDate.ToString("dd MMM yyyy")
                                };
                            }
                        }
                        break;

                    case "Attendance Marked":
                    case "Low Attendance Alert":
                    case "Low Attendance Warning":
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
                                var presentCount = student.Attendances.Count(a => a.Status == "Present");
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

                    case "Class Capacity Alert":
                        if (!string.IsNullOrEmpty(notification.RelatedEntityId))
                        {
                            var classInfo = await _context.Classes
                                .Include(c => c.Teacher)
                                    .ThenInclude(t => t.User)
                                .FirstOrDefaultAsync(c => c.ClassId == notification.RelatedEntityId);

                            if (classInfo != null)
                            {
                                var utilizationRate = (classInfo.CurrentCapacity * 100.0 / classInfo.MaxCapacity);

                                detailData = new
                                {
                                    className = classInfo.ClassName,
                                    teacher = classInfo.Teacher?.User?.FullName,
                                    currentEnrollment = classInfo.CurrentCapacity,
                                    maxCapacity = classInfo.MaxCapacity,
                                    utilizationRate = $"{utilizationRate:F1}%",
                                    room = classInfo.RoomNumber
                                };
                            }
                        }
                        break;
                    }
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
            ViewBag.Title = _localization["AdminSettings"];
            ViewBag.Localization = _localization;
            return View(user);
        }

        [Authorize(Roles = "Admin")]
        public IActionResult ChangePassword()
        {
            ViewBag.ActiveMenu = "Settings";
            ViewBag.ActiveSubmenu = "ChangePassword";
            ViewBag.Title = _localization["ChangePassword"];
            ViewBag.Localization = _localization;

            return View();
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            ViewBag.ActiveMenu = "Settings";
            ViewBag.ActiveSubmenu = "ChangePassword";
            ViewBag.Title = _localization["ChangePassword"];
            ViewBag.Localization = _localization;

            // Validate input
            if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
            {
                TempData["ErrorMessage"] = "Please enter all password fields.";
                return View();
            }

            // Verify passwords match
            if (newPassword != confirmPassword)
            {
                TempData["ErrorMessage"] = "Passwords do not match.";
                return View();
            }

            // Validate password strength using Helper method
            var (isValid, errors) = _helper.ValidatePasswordStrength(newPassword);
            if (!isValid)
            {
                TempData["ErrorMessage"] = "Password does not meet security requirements:<br/>" + string.Join("<br/>", errors);
                return View();
            }

            try
            {
                // Get current admin user from database
                var userEmail = User.Identity?.Name;
                if (string.IsNullOrEmpty(userEmail))
                {
                    TempData["ErrorMessage"] = "Unable to identify current user.";
                    return View();
                }

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == userEmail);

                if (user == null || !user.IsActive)
                {
                    TempData["ErrorMessage"] = "User not found or account is inactive.";
                    return View();
                }

                // Verify current password - Try both hashed and plain text for backward compatibility
                bool isPasswordValid = false;

                // First try with proper password hashing (PasswordHasher)
                if (user.PasswordHash.StartsWith("AQA") || user.PasswordHash.Length > 50)
                {
                    // Looks like a hashed password
                    isPasswordValid = _helper.VerifyPassword(user.PasswordHash, currentPassword);
                }
                else
                {
                    // Plain text password (for backward compatibility with existing data)
                    isPasswordValid = user.PasswordHash == currentPassword;
                }

                if (!isPasswordValid)
                {
                    TempData["ErrorMessage"] = "Current password is incorrect.";
                    return View();
                }

                // Hash and update new password
                user.PasswordHash = _helper.HashPassword(newPassword);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Password changed successfully!";

                // Optionally, send a confirmation email
                try
                {
                    _helper.SendPasswordChangeConfirmationEmail(user.Email, user.FullName);
                }
                catch (Exception emailEx)
                {
                    Console.WriteLine($"Confirmation email failed: {emailEx.Message}");
                    // Don't show error to user since password was changed successfully
                }

                return RedirectToAction("ChangePassword");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Change password error: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while changing your password. Please try again later.";
                return View();
            }
        }

        // ==================== REQUEST MODELS ====================

        // DTO for attendance summary with "Not Marked" support
        public class AttendanceSummaryDto
        {
            public string? AttendanceId { get; set; }
            public required string StudentId { get; set; }
            public required string ClassId { get; set; }
            public required DateTime Date { get; set; }
            public required string Status { get; set; } // "Present", "Absent", "Late", "Leave", "Not Marked"
            public DateTime? TakenOn { get; set; }
            public string? MarkedByTeacherId { get; set; }
            public bool Flag { get; set; }
        }

        // Request models for bulk operations
        public class BulkAttendanceRequest
        {
            public required string ClassId { get; set; }
            public required string PinCode { get; set; }
            public required string Date { get; set; }
            public required List<AttendanceItem> Attendances { get; set; }
        }

        public class AttendanceItem
        {
            public required string StudentId { get; set; }
            public required string Status { get; set; }
        }

        // Request model for manual attendance saving
        public class ManualAttendanceRequest
        {
            public required string ClassId { get; set; }
            public string? PinCode { get; set; }  // Optional - only required for student-initiated attendance
            public required string Date { get; set; }
            public required List<ManualAttendanceItem> Attendances { get; set; }
        }

        public class ManualAttendanceItem
        {
            public required string StudentId { get; set; }
            public required string Status { get; set; }
        }

        // Request model for single attendance saving
        public class SingleAttendanceRequest
        {
            public required string ClassId { get; set; }
            public required string StudentId { get; set; }
            public required string Date { get; set; }
            public required string Status { get; set; }
        }

        // Helper method to send absence notifications to student and parent
        private async Task SendAbsenceNotification(string studentId, string classId, DateTime date)
        {
            try
            {
                // Get student with parent and class information
                var student = await _context.Students
                    .Include(s => s.User)
                    .Include(s => s.Parent)
                        .ThenInclude(p => p.User)
                    .FirstOrDefaultAsync(s => s.StudentId == studentId);

                var classInfo = await _context.Classes
                    .Include(c => c.Subject)
                    .FirstOrDefaultAsync(c => c.ClassId == classId);

                if (student == null || classInfo == null)
                {
                    return;
                }

                var className = classInfo.ClassName;
                var subjectName = classInfo.Subject?.SubjectName ?? "";
                var dateStr = date.ToString("MMM dd, yyyy");

                // Create notification for the student
                var studentNotificationId = IdGenerator.GenerateNotificationId(_context);
                var studentNotification = new Notification
                {
                    NotificationId = studentNotificationId,
                    UserId = student.UserId,
                    Description = $"You were marked absent for {className} ({subjectName}) on {dateStr}.",
                    Status = "unread",
                    CreatedDate = DateTime.Now
                };
                _context.Notifications.Add(studentNotification);

                // Create notification for the parent if exists
                if (student.Parent != null)
                {
                    var parentNotificationId = IdGenerator.GenerateNotificationId(_context);
                    var parentNotification = new Notification
                    {
                        NotificationId = parentNotificationId,
                        UserId = student.Parent.UserId,
                        Description = $"{student.User.FullName} was marked absent for {className} ({subjectName}) on {dateStr}.",
                        Status = "unread",
                        CreatedDate = DateTime.Now
                    };
                    _context.Notifications.Add(parentNotification);
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Log error but don't fail the attendance marking
                Console.WriteLine($"Error sending absence notification: {ex.Message}");
            }
        }

        /// <summary>
        /// Check class capacity and send notification to admin if at 90% or above
        /// </summary>
        private async Task CheckAndNotifyClassCapacity(string classId)
        {
            var classInfo = await _context.Classes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ClassId == classId);

            if (classInfo != null && classInfo.CurrentCapacity >= classInfo.MaxCapacity * 0.9)
            {
                var adminUsers = await _context.Users
                    .Where(u => u.UserType == "Admin")
                    .ToListAsync();

                foreach (var admin in adminUsers)
                {
                    var capacityNotificationId = IdGenerator.GenerateNotificationId(_context);
                    var capacityPercentage = (int)Math.Round((double)classInfo.CurrentCapacity / classInfo.MaxCapacity * 100);
                    var capacityNotification = new Notification
                    {
                        NotificationId = capacityNotificationId,
                        UserId = admin.UserId,
                        Type = "Class Capacity Alert",
                        Description = $"Class {classInfo.ClassName} is at {classInfo.CurrentCapacity}/{classInfo.MaxCapacity} capacity ({capacityPercentage}%)",
                        RelatedEntityId = classInfo.ClassId,
                        Status = "unread",
                        CreatedDate = DateTime.Now
                    };
                    _context.Notifications.Add(capacityNotification);
                }
            }
        }
    }
}
    