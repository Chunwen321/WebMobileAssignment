using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebMobileAssignment.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace TuitionAttendanceSystem.Controllers
{
    public class AdminController : Controller
    {
        private readonly DB _context;

        public AdminController(DB context)
        {
            _context = context;
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StudentCreate(string fullName, string email, string password,
            string parentId, string classId, DateTime? dateOfBirth, string gender)
        {
            // Clear ModelState for optional fields that might be empty strings
            if (string.IsNullOrEmpty(parentId))
            {
                ModelState.Remove("parentId");
                parentId = null;
            }

            if (string.IsNullOrEmpty(classId))
            {
                ModelState.Remove("classId");
                classId = null;
            }

            // Manual validation for required fields
            if (string.IsNullOrWhiteSpace(fullName))
                ModelState.AddModelError("fullName", "Full name is required");

            if (string.IsNullOrWhiteSpace(email))
                ModelState.AddModelError("email", "Email is required");

            if (string.IsNullOrWhiteSpace(password))
                ModelState.AddModelError("password", "Password is required");

            if (string.IsNullOrWhiteSpace(gender))
                ModelState.AddModelError("gender", "Gender is required");

            if (!dateOfBirth.HasValue || dateOfBirth.Value == default(DateTime))
                ModelState.AddModelError("dateOfBirth", "Date of birth is required");

            if (ModelState.IsValid)
            {
                try
                {
                    // Generate IDs
                    var studentCount = await _context.Students.CountAsync();
                    var userId = $"STU{(studentCount + 1):D3}";
                    var studentId = userId;

                    // Create User
                    var user = new User
                    {
                        UserId = userId,
                        FullName = fullName,
                        Email = email,
                        PasswordHash = password, // TODO: Implement BCrypt.Net.BCrypt.HashPassword(password) for production
                        UserType = "Student",
                        CreatedDate = DateTime.Now,
                        IsActive = true
                    };
                    _context.Users.Add(user);

                    // Create Student
                    var student = new Student
                    {
                        StudentId = studentId,
                        UserId = userId,
                        ParentId = parentId,
                        ClassId = classId,
                        DateOfBirth = dateOfBirth.Value,
                        Gender = gender
                    };
                    _context.Students.Add(student);

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Student added successfully!";
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
            ViewBag.DateOfBirth = dateOfBirth?.ToString("yyyy-MM-dd");
            ViewBag.Gender = gender;
            ViewBag.ParentId = parentId;
            ViewBag.ClassId = classId;
            ViewBag.Parents = await _context.Parents.Include(p => p.User).ToListAsync();
            ViewBag.Classes = await _context.Classes.ToListAsync();
            return View("AddStudent");
        }

        public async Task<IActionResult> StudentEdit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var student = await _context.Students
                .Include(s => s.User)
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
            string parentId, string classId, DateTime dateOfBirth, string gender)
        {
            var student = await _context.Students
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.StudentId == studentId);

            if (student == null)
            {
                TempData["ErrorMessage"] = "Student not found.";
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    student.User.FullName = fullName;
                    student.User.Email = email;
                    student.ParentId = string.IsNullOrEmpty(parentId) ? null : parentId;
                    student.ClassId = string.IsNullOrEmpty(classId) ? null : classId;
                    student.DateOfBirth = dateOfBirth;
                    student.Gender = gender;

                    _context.Update(student);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Student '{fullName}' updated successfully!";
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
                .FirstOrDefaultAsync(m => m.StudentId == id);

            if (student == null) return NotFound();

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
                    .FirstOrDefaultAsync(s => s.StudentId == id);

                if (student != null)
                {
                    var studentName = student.User.FullName;
                    _context.Users.Remove(student.User); // Cascade delete will remove student
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
        public async Task<IActionResult> TeacherCreate(string fullName, string email, string password,
            string phoneNumber, string subjectTeach, DateTime? hireDate)
        {
            // Manual validation for required fields
            if (string.IsNullOrWhiteSpace(fullName))
                ModelState.AddModelError("fullName", "Full name is required");

            if (string.IsNullOrWhiteSpace(email))
                ModelState.AddModelError("email", "Email is required");

            if (string.IsNullOrWhiteSpace(password))
                ModelState.AddModelError("password", "Password is required");

            if (!hireDate.HasValue || hireDate.Value == default(DateTime))
                ModelState.AddModelError("hireDate", "Hire date is required");

            if (ModelState.IsValid)
            {
                try
                {
                    var teacherCount = await _context.Teachers.CountAsync();
                    var userId = $"TEACH{(teacherCount + 1):D3}";
                    var teacherId = userId;

                    var user = new User
                    {
                        UserId = userId,
                        FullName = fullName,
                        Email = email,
                        PasswordHash = password, // TODO: Implement BCrypt.Net.BCrypt.HashPassword(password)
                        UserType = "Teacher",
                        CreatedDate = DateTime.Now,
                        IsActive = true
                    };
                    _context.Users.Add(user);

                    var teacher = new Teacher
                    {
                        TeacherId = teacherId,
                        UserId = userId,
                        PhoneNumber = phoneNumber,
                        SubjectTeach = subjectTeach,
                        HireDate = hireDate.Value
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

            ViewBag.ActiveMenu = "TeacherManagement";
            ViewBag.Title = "Create Teacher";
            ViewBag.FullName = fullName;
            ViewBag.Email = email;
            ViewBag.PhoneNumber = phoneNumber;
            ViewBag.SubjectTeach = subjectTeach;
            ViewBag.HireDate = hireDate?.ToString("yyyy-MM-dd");
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
        public async Task<IActionResult> TeacherEdit(string teacherId, string fullName, string email,
            string phoneNumber, string subjectTeach, DateTime hireDate)
        {
            var teacher = await _context.Teachers
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TeacherId == teacherId);

            if (teacher == null)
            {
                TempData["ErrorMessage"] = "Teacher not found.";
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    teacher.User.FullName = fullName;
                    teacher.User.Email = email;
                    teacher.PhoneNumber = phoneNumber;
                    teacher.SubjectTeach = subjectTeach;
                    teacher.HireDate = hireDate;

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
                .FirstOrDefaultAsync(m => m.TeacherId == id);

            if (teacher == null) return NotFound();

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
        public async Task<IActionResult> ParentCreate(string fullName, string email, string password, string phoneNumber)
        {
            // Manual validation for required fields
            if (string.IsNullOrWhiteSpace(fullName))
                ModelState.AddModelError("fullName", "Full name is required");

            if (string.IsNullOrWhiteSpace(email))
                ModelState.AddModelError("email", "Email is required");

            if (string.IsNullOrWhiteSpace(password))
                ModelState.AddModelError("password", "Password is required");

            if (ModelState.IsValid)
            {
                try
                {
                    var parentCount = await _context.Parents.CountAsync();
                    var userId = $"PARENT{(parentCount + 1):D3}";
                    var parentId = userId;

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
            ViewBag.PhoneNumber = phoneNumber;
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

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClassCreate(string className, string teacherId, string roomNumber, string scheduleInfo)
        {
            if (ModelState.IsValid)
            {
                var classCount = await _context.Classes.CountAsync();
                var classId = $"CLASS{(classCount + 1):D3}";

                var @class = new Class
                {
                    ClassId = classId,
                    ClassName = className,
                    TeacherId = teacherId,
                    RoomNumber = roomNumber,
                    ScheduleInfo = scheduleInfo
                };
                _context.Classes.Add(@class);

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(ClassIndex));
            }

            ViewBag.ActiveMenu = "ClassManagement";
            ViewBag.ActiveSubmenu = "Classes";
            ViewBag.Teachers = await _context.Teachers.Include(t => t.User).ToListAsync();
            return View();
        }

        // ==================== ATTENDANCE MANAGEMENT ====================
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
        public IActionResult Settings()
        {
            ViewBag.ActiveMenu = "Settings";
            ViewBag.Title = "Settings";

            return View();
        }
    }
}