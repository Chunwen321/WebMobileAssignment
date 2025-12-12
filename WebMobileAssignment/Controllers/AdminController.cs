using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebMobileAssignment.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace WebMobileAssignment.Controllers
{
    public class AdminController : Controller
    {
        private readonly DB _context;
        private readonly Helper _helper;

        public AdminController(DB context, Helper helper)
        {
            _context = context;
            _helper = helper;
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

            var presentToday = await _context.Attendances
                .Where(a => a.Date.Date == DateTime.Today && a.Status == "Present")
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

            return Json(new { 
                success = true, 
                parentId = parent.ParentId, 
                fullName = parent.User.FullName,
                email = parent.User.Email,
                phone = parent.PhoneNumber ?? ""
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StudentCreate(string fullName, string email, string password,
            string parentId, List<string>? classIds, DateTime? dateOfBirth, string gender,
            string? phoneNumber, string status, bool isActive, DateTime? enrollmentDate,
            // New parent fields
            string newParentFullName, string newParentEmail, string newParentPassword, string newParentPhone)
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

            // Manual validation for required fields
            if (string.IsNullOrWhiteSpace(fullName))
                ModelState.AddModelError("fullName", "Full name is required");

            if (string.IsNullOrWhiteSpace(email))
                ModelState.AddModelError("email", "Email is required");

            if (string.IsNullOrWhiteSpace(password))
                ModelState.AddModelError("password", "Password is required");

            if (password != null && password.Length < 8)
                ModelState.AddModelError("password", "Password must be at least 8 characters long");

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
                if (string.IsNullOrWhiteSpace(newParentPassword))
                    ModelState.AddModelError("newParentPassword", "Parent password is required when creating a new parent");
            }
            else
            {
                // If we have a parentId (existing parent), remove validation errors for new parent fields
                ModelState.Remove("newParentFullName");
                ModelState.Remove("newParentEmail");
                ModelState.Remove("newParentPassword");
                ModelState.Remove("newParentPhone");
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
                    // If need to create new parent
                    if (string.IsNullOrEmpty(parentId) && !string.IsNullOrWhiteSpace(newParentEmail))
                    {
                        // create parent user and parent
                        // Generate proper User ID format
       var userCount = await _context.Users.CountAsync();
      var parentUserId = $"U{(userCount + 1):D4}";
    
    var parentCount = await _context.Parents.CountAsync();
     var parentIdGen = $"P{(parentCount + 1):D4}";

                       var parentUser = new User
          {
                UserId = parentUserId,
       FullName = newParentFullName,
         Email = newParentEmail,
   PasswordHash = newParentPassword,
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
            PhoneNumber = newParentPhone
            };
     _context.Parents.Add(parent);

       // Save to get parent in DB
      await _context.SaveChangesAsync();

   parentId = parent.ParentId;
        }

                    // Generate IDs for student
                    var studentCount = await _context.Students.CountAsync();
                    var userId = $"STU{(studentCount + 1):D3}";
                    var studentId = userId;

                    // Create User with all fields
                    var user = new User
                    {
                        UserId = userId,
                        FullName = fullName,
                        Email = email,
                        PasswordHash = password, // Plain text for now - hash on first login or via utility
                        PhoneNumber = phoneNumber,
                        DateOfBirth = dateOfBirth,
                        Gender = gender,
                        UserType = "Student",
                        CreatedDate = DateTime.Now,
                        Status = status,
                        IsActive = isActive
                    };
                    _context.Users.Add(user);

                    // Create Student with all fields
                    var student = new Student
                    {
                        StudentId = studentId,
                        UserId = userId,
                        ParentId = parentId,
                        ClassId = null, // Don't use ClassId anymore - use Enrollments
                        DateOfBirth = dateOfBirth.Value,
                        Gender = gender,
                        EnrollmentDate = enrollmentDate ?? DateTime.Now
                    };
                    _context.Students.Add(student);

                    await _context.SaveChangesAsync();

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

                    TempData["SuccessMessage"] = $"Student '{fullName}' added successfully with {enrolledCount} class enrollment(s)!";

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
            DateTime dateOfBirth, string gender, string status)
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
                    // Update user information
                    student.User.FullName = fullName;
                    student.User.Email = email;
                    student.User.PhoneNumber = phoneNumber;
                    student.User.Status = status;
                    
                    // Update student information
                    student.ParentId = string.IsNullOrEmpty(parentId) ? null : parentId;
                    student.DateOfBirth = dateOfBirth;
                    student.Gender = gender;
                    
                    // Don't set ClassId on Student anymore - use Enrollments instead
                    student.ClassId = null;

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

            // Get attendance statistics
            var attendanceStats = await _context.Attendances
                .Where(a => a.StudentId == id)
                .GroupBy(a => a.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var totalAttendance = attendanceStats.Sum(s => s.Count);
            var presentCount = attendanceStats.FirstOrDefault(s => s.Status == "Present")?.Count ?? 0;
            var absentCount = attendanceStats.FirstOrDefault(s => s.Status == "Absent")?.Count ?? 0;
            var lateCount = attendanceStats.FirstOrDefault(s => s.Status == "Late")?.Count ?? 0;
            var attendanceRate = totalAttendance > 0 ? Math.Round((decimal)presentCount / totalAttendance * 100, 1) : 0;

            ViewBag.TotalEnrollments = student.Enrollments?.Count ?? 0;
            ViewBag.TotalAttendance = totalAttendance;
            ViewBag.PresentCount = presentCount;
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
            string fullName, string email, string password,
            string? phoneNumber, string? subjectTeach, DateTime? hireDate,
            string? title, string? education, string? skill, string? bio,
            DateTime? dateOfBirth, string? gender, string? status)
        {
            // Manual validation for required fields
            if (string.IsNullOrWhiteSpace(fullName))
                ModelState.AddModelError("fullName", "Full name is required");

            if (string.IsNullOrWhiteSpace(email))
                ModelState.AddModelError("email", "Email is required");

            if (string.IsNullOrWhiteSpace(password))
                ModelState.AddModelError("password", "Password is required");

            if (password != null && password.Length < 8)
                ModelState.AddModelError("password", "Password must be at least 8 characters long");

            if (!hireDate.HasValue)
                ModelState.AddModelError("hireDate", "Hire date is required");

            // Set default status if not provided
            if (string.IsNullOrWhiteSpace(status))
            {
                status = "active";
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var teacherCount = await _context.Teachers.CountAsync();
                    var userId = $"TEACH{(teacherCount + 1):D3}";
                    var teacherId = userId;

                    // Create User with all fields including optional ones
                    var user = new User
                    {
                        UserId = userId,
                        FullName = fullName,
                        Email = email,
                        PasswordHash = password, // Plain text for now - hash on first login or via utility
                        PhoneNumber = phoneNumber,
                        DateOfBirth = dateOfBirth,
                        Gender = gender,
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
                    TempData["SuccessMessage"] = $"Teacher '{fullName}' added successfully!";
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
            DateTime? dateOfBirth, string? gender, string? status)
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

            if (ModelState.IsValid)
            {
                try
                {
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

            // Get attendance statistics marked by this teacher
            var attendanceStats = await _context.Attendances
                .Where(a => a.MarkedByTeacherId == id)
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
<<<<<<< Updated upstream
<<<<<<< Updated upstream
        public async Task<IActionResult> ParentCreate(string fullName, string email, string password, string phoneNumber)
=======
     public async Task<IActionResult> ParentCreate(string fullName, string email, string password, 
        string? phoneNumber, string? address, DateTime? dateOfBirth, string? gender)
>>>>>>> Stashed changes
=======
     public async Task<IActionResult> ParentCreate(string fullName, string email, string password, 
        string? phoneNumber, string? address, DateTime? dateOfBirth, string? gender)
>>>>>>> Stashed changes
        {
            // Manual validation for required fields
       if (string.IsNullOrWhiteSpace(fullName))
       ModelState.AddModelError("fullName", "Full name is required");

            if (string.IsNullOrWhiteSpace(email))
   ModelState.AddModelError("email", "Email is required");

            if (string.IsNullOrWhiteSpace(password))
                ModelState.AddModelError("password", "Password is required");

<<<<<<< Updated upstream
<<<<<<< Updated upstream
            if (ModelState.IsValid)
=======
   if (password != null && password.Length < 8)
        ModelState.AddModelError("password", "Password must be at least 8 characters long");

          if (ModelState.IsValid)
>>>>>>> Stashed changes
            {
   try
    {
           // Generate proper User ID format
            var userCount = await _context.Users.CountAsync();
              var userId = $"U{(userCount + 1):D4}";
        
    var parentCount = await _context.Parents.CountAsync();
  var parentId = $"P{(parentCount + 1):D4}";

<<<<<<< Updated upstream
                    var user = new User
                    {
                        UserId = userId,
                        FullName = fullName,
                        Email = email,
                        PasswordHash = password, // TODO: Implement BCrypt.Net.BCrypt.HashPassword(password)
                        UserType = "Parent",
                        CreatedDate = DateTime.Now,
                        IsActive = true
                    };
                    _context.Users.Add(user);

                    var parent = new Parent
                    {
                        ParentId = parentId,
                        UserId = userId,
                        PhoneNumber = phoneNumber
                    };
                    _context.Parents.Add(parent);
=======
       var user = new User
              {
     UserId = userId,
          FullName = fullName,
          Email = email,
    PasswordHash = password, // Plain text for now - hash on first login or via utility
            PhoneNumber = phoneNumber,
   DateOfBirth = dateOfBirth,
        Gender = gender,
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
            PhoneNumber = phoneNumber,
   Address = address
            };
       _context.Parents.Add(parent);
>>>>>>> Stashed changes

=======
   if (password != null && password.Length < 8)
        ModelState.AddModelError("password", "Password must be at least 8 characters long");

          if (ModelState.IsValid)
            {
   try
    {
           // Generate proper User ID format
            var userCount = await _context.Users.CountAsync();
              var userId = $"U{(userCount + 1):D4}";
        
    var parentCount = await _context.Parents.CountAsync();
  var parentId = $"P{(parentCount + 1):D4}";

       var user = new User
              {
     UserId = userId,
          FullName = fullName,
          Email = email,
    PasswordHash = password, // Plain text for now - hash on first login or via utility
            PhoneNumber = phoneNumber,
   DateOfBirth = dateOfBirth,
        Gender = gender,
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
            PhoneNumber = phoneNumber,
   Address = address
            };
       _context.Parents.Add(parent);

>>>>>>> Stashed changes
     await _context.SaveChangesAsync();
  TempData["SuccessMessage"] = $"Parent '{fullName}' added successfully!";
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
<<<<<<< Updated upstream
<<<<<<< Updated upstream
            ViewBag.PhoneNumber = phoneNumber;
            return View();
=======
=======
>>>>>>> Stashed changes
         ViewBag.PhoneNumber = phoneNumber;
            ViewBag.Address = address;
       ViewBag.DateOfBirth = dateOfBirth?.ToString("yyyy-MM-dd");
         ViewBag.Gender = gender;
    return View();
<<<<<<< Updated upstream
>>>>>>> Stashed changes
=======
>>>>>>> Stashed changes
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
        public async Task<IActionResult> ParentEdit(string parentId, string fullName, string email, string phoneNumber)
        {
            var parent = await _context.Parents
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.ParentId == parentId);

            if (parent == null)
            {
                TempData["ErrorMessage"] = "Parent not found.";
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    parent.User.FullName = fullName;
                    parent.User.Email = email;
                    parent.PhoneNumber = phoneNumber;

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
            string day, string startTime, string endTime, string subjectId)
        {
            if (ModelState.IsValid)
            {
                var classCount = await _context.Classes.CountAsync();
                var classId = $"CLASS{(classCount + 1):D3}";

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
                    EndTime = parsedEndTime
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
        // ==================== ATTENDANCE MANAGEMENT ====================
        
        // Take Attendance with PIN Code
        public async Task<IActionResult> AttendanceTake()
        {
            ViewBag.ActiveMenu = "AttendanceManagement";
            ViewBag.ActiveSubmenu = "Take";
            ViewBag.Title = "Take Attendance";
            
            // Load all classes
            ViewBag.Classes = await _context.Classes
                .Include(c => c.Teacher)
                .ThenInclude(t => t.User)
                .Include(c => c.Enrollments)
                .OrderBy(c => c.ClassName)
                .ToListAsync();
            
            // Load all existing sessions
            ViewBag.Sessions = await _context.AttendanceSessions
                .Where(s => s.IsActive)
                .ToListAsync();
            
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GenerateAttendancePin(string classId)
        {
            try
            {
                var classEntity = await _context.Classes
                    .Include(c => c.Enrollments)
                    .FirstOrDefaultAsync(c => c.ClassId == classId);

                if (classEntity == null)
                    return Json(new { success = false, message = "Class not found" });

                // Check if class has schedule (required for time validation)
                if (!classEntity.StartTime.HasValue || !classEntity.EndTime.HasValue || string.IsNullOrEmpty(classEntity.Day))
                    return Json(new { success = false, message = "Class schedule not configured. Please set class day and time first." });

                // Check if PIN already exists for this class
                var existingSession = await _context.AttendanceSessions
                    .FirstOrDefaultAsync(s => s.ClassId == classId);

                if (existingSession != null)
                    return Json(new { success = false, message = "PIN code has already been generated for this class. Each class can only have one PIN code." });

                // Generate 6-digit PIN
                var random = new Random();
                var pinCode = random.Next(100000, 999999).ToString();

                // Create session - expiry based on class end time
                var sessionCount = await _context.AttendanceSessions.CountAsync();
                var sessionId = $"SESSION{(sessionCount + 1):D5}";

                // Calculate expiry date (class end time on the current day or next occurrence of class day)
                var today = DateTime.Today;
                var classEndTime = today.Add(classEntity.EndTime.Value);
                
                // If class end time has passed today, set expiry to next week's class
                if (DateTime.Now > classEndTime)
                {
                    classEndTime = classEndTime.AddDays(7);
                }

                var session = new AttendanceSession
                {
                    SessionId = sessionId,
                    PinCode = pinCode,
                    ClassId = classId,
                    CreatedByTeacherId = null, // Set to current teacher if auth implemented
                    CreatedDate = DateTime.Now,
                    ExpiryDate = classEndTime, // Valid until class end time
                    IsActive = true,
                    SessionType = "Class"
                };

                _context.AttendanceSessions.Add(session);
                await _context.SaveChangesAsync();

                // Get server URL for QR code
                var request = HttpContext.Request;
                var baseUrl = $"{request.Scheme}://{request.Host}";
                var qrUrl = $"{baseUrl}/Admin/AttendancePinEntry?pin={pinCode}";

                return Json(new
                {
                    success = true,
                    sessionId = sessionId,
                    pinCode = pinCode,
                    qrUrl = qrUrl,
                    expiryDate = session.ExpiryDate,
                    className = classEntity.ClassName,
                    enrolledCount = classEntity.Enrollments.Count,
                    classDay = classEntity.Day,
                    startTime = classEntity.StartTime.Value.ToString("hh\\:mm"),
                    endTime = classEntity.EndTime.Value.ToString("hh\\:mm")
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // PIN Entry Page (Mobile-friendly)
        public async Task<IActionResult> AttendancePinEntry(string? pin)
        {
            ViewBag.PrefilledPin = pin;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SubmitAttendancePin(string pinCode, string studentId)
        {
            try
            {
                // Find active session with this PIN
                var session = await _context.AttendanceSessions
                    .Include(s => s.Class)
                    .FirstOrDefaultAsync(s => s.PinCode == pinCode && s.IsActive);

                if (session == null)
                    return Json(new { success = false, message = "Invalid PIN code" });

                // Validate class has schedule
                if (!session.Class.StartTime.HasValue || !session.Class.EndTime.HasValue)
                    return Json(new { success = false, message = "Class schedule not configured" });

                // Check if current time is within class hours
                var currentTime = DateTime.Now.TimeOfDay;
                var classStartTime = session.Class.StartTime.Value;
                var classEndTime = session.Class.EndTime.Value;

                if (currentTime < classStartTime || currentTime > classEndTime)
                {
                    return Json(new { 
                        success = false, 
                        message = $"Attendance can only be taken during class hours ({classStartTime:hh\\:mm} - {classEndTime:hh\\:mm}). Current time: {DateTime.Now:hh\\:mm tt}" 
                    });
                }

                // Check if today matches the class day
                var currentDayOfWeek = DateTime.Now.DayOfWeek.ToString();
                if (!string.IsNullOrEmpty(session.Class.Day) && !session.Class.Day.Equals(currentDayOfWeek, StringComparison.OrdinalIgnoreCase))
                {
                    return Json(new { 
                        success = false, 
                        message = $"This class is scheduled for {session.Class.Day}, not {currentDayOfWeek}" 
                    });
                }

                // Check if student exists and is enrolled in this class
                var student = await _context.Students
                    .Include(s => s.User)
                    .Include(s => s.Enrollments)
                    .FirstOrDefaultAsync(s => s.StudentId == studentId);

                if (student == null)
                    return Json(new { success = false, message = "Student not found" });

                var isEnrolled = student.Enrollments.Any(e => e.ClassId == session.ClassId);
                if (!isEnrolled)
                    return Json(new { success = false, message = "Student not enrolled in this class" });

                // Check if already marked attendance for today
                var existingAttendance = await _context.Attendances
                    .FirstOrDefaultAsync(a => a.StudentId == studentId && 
                                              a.ClassId == session.ClassId && 
                                              a.Date.Date == DateTime.Today);

                if (existingAttendance != null)
                    return Json(new { success = false, message = "Attendance already marked for today" });

                // Create attendance record
                var attCount = await _context.Attendances.CountAsync();
                var attId = $"ATT{(attCount + 1):D5}";

                var attendance = new Attendance
                {
                    AttendanceId = attId,
                    StudentId = studentId,
                    ClassId = session.ClassId,
                    Date = DateTime.Now,
                    TakenOn = DateTime.Now,
                    Status = "Present",
                    MarkedByTeacherId = session.CreatedByTeacherId
                };

                _context.Attendances.Add(attendance);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"Attendance marked successfully for {student.User.FullName}",
                    studentName = student.User.FullName,
                    className = session.Class.ClassName,
                    time = DateTime.Now.ToString("hh:mm tt")
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Attendance Management (View/Edit Records)
        public async Task<IActionResult> AttendanceManagement(string? classId, DateTime? date)
        {
            ViewBag.ActiveMenu = "AttendanceManagement";
            ViewBag.ActiveSubmenu = "Manage";
            ViewBag.Title = "Manage Attendance";

            var selectedDate = date ?? DateTime.Today;
            ViewBag.SelectedDate = selectedDate.ToString("yyyy-MM-dd");

            var query = _context.Attendances
                .Include(a => a.Student)
                .ThenInclude(s => s.User)
                .Include(a => a.Class)
                .Where(a => a.Date.Date == selectedDate.Date);

            if (!string.IsNullOrEmpty(classId))
            {
                query = query.Where(a => a.ClassId == classId);
                ViewBag.SelectedClassId = classId;
            }

            var attendances = await query.OrderBy(a => a.Class.ClassName)
                                          .ThenBy(a => a.Student.User.FullName)
                                          .ToListAsync();

            ViewBag.Classes = await _context.Classes.ToListAsync();

            return View(attendances);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateAttendanceStatus(string attendanceId, string status)
        {
            try
            {
                var attendance = await _context.Attendances.FindAsync(attendanceId);
                if (attendance == null)
                    return Json(new { success = false, message = "Attendance record not found" });

                attendance.Status = status;
                attendance.TakenOn = DateTime.Now;
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Status updated successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Attendance Records/History
        public async Task<IActionResult> AttendanceRecords(string? studentId, string? classId, DateTime? startDate, DateTime? endDate)
        {
            ViewBag.ActiveMenu = "AttendanceManagement";
            ViewBag.ActiveSubmenu = "Records";
            ViewBag.Title = "Attendance Records";

            var query = _context.Attendances
                .Include(a => a.Student)
                .ThenInclude(s => s.User)
                .Include(a => a.Class)
                .Include(a => a.MarkedByTeacher)
                .ThenInclude(t => t.User)
                .AsQueryable();

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

            var records = await query.OrderByDescending(a => a.Date)
                                     .ThenBy(a => a.Student.User.FullName)
                                     .ToListAsync();

            ViewBag.Students = await _context.Students.Include(s => s.User).ToListAsync();
            ViewBag.Classes = await _context.Classes.ToListAsync();

            // Calculate statistics
            var totalRecords = records.Count;
            var presentCount = records.Count(r => r.Status == "Present");
            var absentCount = records.Count(r => r.Status == "Absent");
            var lateCount = records.Count(r => r.Status == "Late");
            var attendanceRate = totalRecords > 0 ? Math.Round((decimal)presentCount / totalRecords * 100, 1) : 0;

            ViewBag.TotalRecords = totalRecords;
            ViewBag.PresentCount = presentCount;
            ViewBag.AbsentCount = absentCount;
            ViewBag.LateCount = lateCount;
            ViewBag.AttendanceRate = attendanceRate;

            return View(records);
        }

        public async Task<IActionResult> AttendanceIndex(string classId, DateTime? startDate, DateTime? endDate)
        {
            ViewBag.ActiveMenu = "AttendanceManagement";
            ViewBag.ActiveSubmenu = "Records";
            ViewBag.Title = "Attendance Records";

            var attendances = _context.Attendances
                .Include(a => a.Student)
                .ThenInclude(s => s.User)
                .Include(a => a.Class)
                .Include(a => a.MarkedByTeacher)
                .ThenInclude(t => t.User)
                .AsQueryable();

            if (!string.IsNullOrEmpty(classId))
                attendances = attendances.Where(a => a.ClassId == classId);

            if (startDate.HasValue)
                attendances = attendances.Where(a => a.Date >= startDate);

            if (endDate.HasValue)
                attendances = attendances.Where(a => a.Date <= endDate);

            ViewBag.Classes = await _context.Classes.ToListAsync();

            return View(await attendances.OrderByDescending(a => a.Date).ToListAsync());
        }

        public async Task<IActionResult> AttendanceCreate()
        {
            ViewBag.ActiveMenu = "AttendanceManagement";
            ViewBag.ActiveSubmenu = "Record";
            ViewBag.Title = "Record Attendance";
            ViewBag.Students = await _context.Students.Include(s => s.User).ToListAsync();
            ViewBag.Classes = await _context.Classes.ToListAsync();
            ViewBag.Teachers = await _context.Teachers.Include(t => t.User).ToListAsync();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AttendanceCreate(string studentId, string classId,
            DateTime date, string status, string markedByTeacherId)
        {
            if (ModelState.IsValid)
            {
                var attCount = await _context.Attendances.CountAsync();
                var attId = $"ATT{(attCount + 1):D5}";

                var attendance = new Attendance
                {
                    AttendanceId = attId,
                    StudentId = studentId,
                    ClassId = classId,
                    Date = date,
                    Status = status,
                    MarkedByTeacherId = markedByTeacherId
                };
                _context.Attendances.Add(attendance);

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(AttendanceIndex));
            }

            ViewBag.Students = await _context.Students.Include(s => s.User).ToListAsync();
            ViewBag.Classes = await _context.Classes.ToListAsync();
            ViewBag.Teachers = await _context.Teachers.Include(t => t.User).ToListAsync();

            return View();
        }

        // ==================== REPORTS ====================
        public async Task<IActionResult> Reports()
        {
            ViewBag.ActiveMenu = "Reports";
            ViewBag.Title = "Reports & Analytics";

            var totalStudents = await _context.Students.CountAsync();
            var totalTeachers = await _context.Teachers.CountAsync();
            var totalClasses = await _context.Classes.CountAsync();
            var totalAttendanceRecords = await _context.Attendances.CountAsync();

            var thisMonthAttendance = await _context.Attendances
                .Where(a => a.Date.Month == DateTime.Today.Month
                    && a.Date.Year == DateTime.Today.Year)
                .ToListAsync();

            var thisMonthPresent = thisMonthAttendance.Count(a => a.Status == "Present");
            var thisMonthAbsent = thisMonthAttendance.Count(a => a.Status == "Absent");
            var thisMonthLate = thisMonthAttendance.Count(a => a.Status == "Late");
            var thisMonthRate = thisMonthAttendance.Count() > 0
                ? (thisMonthPresent * 100m / thisMonthAttendance.Count()).ToString("F2")
                : "0";

            ViewBag.TotalStudents = totalStudents;
            ViewBag.TotalTeachers = totalTeachers;
            ViewBag.TotalClasses = totalClasses;
            ViewBag.TotalAttendanceRecords = totalAttendanceRecords;
            ViewBag.ThisMonthRate = thisMonthRate;
            ViewBag.ThisMonthPresent = thisMonthPresent;
            ViewBag.ThisMonthAbsent = thisMonthAbsent;
            ViewBag.ThisMonthLate = thisMonthLate;

            return View();
        }

        // ==================== SETTINGS ====================
        public async Task<IActionResult> Settings()
        {
            ViewBag.ActiveMenu = "Settings";
            ViewBag.ActiveSubmenu = "Settings";
            ViewBag.Title = "Admin Settings";

            // Get admin user from Users table (filter by UserType = "Admin")
            var adminUser = await _context.Users
                .Where(u => u.UserType == "Admin")
                .FirstOrDefaultAsync();

            if (adminUser == null)
            {
                TempData["ErrorMessage"] = "No admin account found.";
                return RedirectToAction("Dashboard");
            }

            return View(adminUser);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(string userId, string fullName, string email, string? phoneNumber)
        {
            var adminUser = await _context.Users
                .Where(u => u.UserId == userId && u.UserType == "Admin")
                .FirstOrDefaultAsync();

            if (adminUser == null)
            {
                TempData["ErrorMessage"] = "Admin not found.";
                return RedirectToAction("Settings");
            }

            try
            {
                adminUser.FullName = fullName;
                adminUser.Email = email;
                adminUser.PhoneNumber = phoneNumber;
                
                _context.Update(adminUser);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Settings updated successfully!";
                return RedirectToAction("Settings");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error updating settings: {ex.Message}";
                return View(adminUser);
            }
        }

        public IActionResult ChangePassword()
        {
            ViewBag.ActiveMenu = "Settings";
            ViewBag.ActiveSubmenu = "ChangePassword";
            ViewBag.Title = "Change Password";

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            ViewBag.ActiveMenu = "Settings";
            ViewBag.ActiveSubmenu = "ChangePassword";
            ViewBag.Title = "Change Password";

            // Validation
            if (string.IsNullOrWhiteSpace(currentPassword))
            {
                TempData["ErrorMessage"] = "Current password is required.";
                return View();
            }

            if (string.IsNullOrWhiteSpace(newPassword))
            {
                TempData["ErrorMessage"] = "New password is required.";
                return View();
            }

            if (newPassword.Length < 8)
            {
                TempData["ErrorMessage"] = "Password must be at least 8 characters long.";
                return View();
            }

            if (newPassword != confirmPassword)
            {
                TempData["ErrorMessage"] = "New password and confirmation password do not match.";
                return View();
            }

            try
            {
                // Get admin user from Users table
                var adminUser = await _context.Users
                    .Where(u => u.UserType == "Admin")
                    .FirstOrDefaultAsync();

                if (adminUser == null)
                {
                    TempData["ErrorMessage"] = "Admin account not found.";
                    return View();
                }

                // Verify current password (in production, use BCrypt to compare hashed passwords)
                if (adminUser.PasswordHash != currentPassword)
                {
                    TempData["ErrorMessage"] = "Current password is incorrect.";
                    return View();
                }

                // Update password (in production, use BCrypt.Net.BCrypt.HashPassword(newPassword))
                adminUser.PasswordHash = newPassword;
                _context.Update(adminUser);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Password changed successfully!";
                return RedirectToAction("ChangePassword");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error changing password: {ex.Message}";
                return View();
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetParentById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return Json(new { success = false });

            var parent = await _context.Parents.Include(p => p.User)
                .FirstOrDefaultAsync(p => p.ParentId == id);

            if (parent == null)
                return Json(new { success = false });

            return Json(new
            {
                success = true,
                parentId = parent.ParentId,
                fullName = parent.User.FullName,
                email = parent.User.Email,
                phone = parent.PhoneNumber
            });
        }
    }
}