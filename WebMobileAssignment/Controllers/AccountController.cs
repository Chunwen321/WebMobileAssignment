using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebMobileAssignment.Models;

namespace WebMobileAssignment.Controllers
{
    public class AccountController : Controller
    {
        private readonly DB _context;
        private readonly Helper _helper;

        public AccountController(DB context, Helper helper)
        {
            _context = context;
            _helper = helper;
        }

        // GET: /Account/Login
        public IActionResult Login()
        {
            return View();
        }

        // GET: /Account/ForgotPassword
        public IActionResult ForgotPassword()
        {
            return View();
        }

        // POST: /Account/ForgotPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(email))
            {
                ViewBag.ErrorMessage = "Please enter your email address.";
                return View();
            }

            try
            {
                // Find user by email
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

                // For security reasons, always show success message even if user not found
                // This prevents email enumeration attacks
                if (user == null || !user.IsActive)
                {
                    ViewBag.SuccessMessage = "If an account with that email exists, you will receive password reset instructions shortly.";
                    return View();
                }

                // Generate a temporary password
                string temporaryPassword = _helper.RandomPassword();

                // Update user's password to the temporary one (hashed)
                user.PasswordHash = _helper.HashPassword(temporaryPassword);
                await _context.SaveChangesAsync();

                // Send email with temporary password using the helper method
                try
                {
                    _helper.SendPasswordResetEmail(email, user.FullName, temporaryPassword);

                    ViewBag.SuccessMessage = "Password reset instructions have been sent to your email address. Please check your inbox and spam folder.";
                }
                catch (Exception emailEx)
                {
                    // Rollback password change if email fails
                    // Note: In production, consider using a transaction or a separate "reset token" approach
                    Console.WriteLine($"Email sending failed: {emailEx.Message}");
                    ViewBag.ErrorMessage = "We encountered an issue sending the reset email. Please try again later or contact support.";
                }

                return View();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Password reset error: {ex.Message}");
                ViewBag.ErrorMessage = "An error occurred while processing your request. Please try again later.";
                return View();
            }
        }

        // POST: /Account/Login
        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.ErrorMessage = "Please enter both email and password.";
                return View();
            }

            try
            {
                // Find user by email
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

                if (user == null)
                {
                    ViewBag.ErrorMessage = "Invalid email or password.";
                    return View();
                }

                // Verify password - Try both hashed and plain text for backward compatibility
                bool isPasswordValid = false;

                // First try with proper password hashing (PasswordHasher)
                if (user.PasswordHash.StartsWith("AQA") || user.PasswordHash.Length > 50)
                {
                    // Looks like a hashed password
                    isPasswordValid = _helper.VerifyPassword(user.PasswordHash, password);
                }
                else
                {
                    // Plain text password (for backward compatibility with existing data)
                    isPasswordValid = user.PasswordHash == password;
                }

                if (!isPasswordValid)
                {
                    ViewBag.ErrorMessage = "Invalid email or password.";
                    return View();
                }

                // Check if user is active
                if (!user.IsActive || user.Status != "active")
                {
                    ViewBag.ErrorMessage = "Your account is not active. Please contact administrator.";
                    return View();
                }

                // Auto-detect user type and redirect accordingly
                string userType = user.UserType.ToLower();

                switch (userType)
                {
                    case "admin":
                        // Verify admin record exists, create if missing
                        var admin = await _context.Admins
                            .FirstOrDefaultAsync(a => a.UserId == user.UserId);

                        if (admin == null)
                        {
                            // Auto-create missing Admin record
                            admin = new Admin
                            {
                                AdminId = user.UserId,
                                UserId = user.UserId
                            };
                            _context.Admins.Add(admin);
                            await _context.SaveChangesAsync();
                        }

                        // Set authentication session/cookie
                        _helper.SignIn(user.Email, "Admin", false);
                        return RedirectToAction("Dashboard", "Admin");

                    case "teacher":
                        // Verify teacher record exists, create if missing
                        var teacher = await _context.Teachers
                            .FirstOrDefaultAsync(t => t.UserId == user.UserId);

                        if (teacher == null)
                        {
                            // Auto-create missing Teacher record
                            teacher = new Teacher
                            {
                                TeacherId = user.UserId,
                                UserId = user.UserId
                            };
                            _context.Teachers.Add(teacher);
                            await _context.SaveChangesAsync();
                        }

                        // Set authentication session/cookie
                        _helper.SignIn(user.Email, "Teacher", false);
                        return RedirectToAction("TeachDashboard", "Teacher");

                    case "student":
                        // Verify student record exists, create if missing
                        var student = await _context.Students
                            .FirstOrDefaultAsync(s => s.UserId == user.UserId);

                        if (student == null)
                        {
                            // Auto-create missing Student record
                            student = new Student
                            {
                                StudentId = user.UserId,
                                UserId = user.UserId,
                                EnrollmentDate = DateTime.Now
                            };
                            _context.Students.Add(student);
                            await _context.SaveChangesAsync();
                        }

                        // Set authentication session/cookie
                        _helper.SignIn(user.Email, "Student", false);
                        return RedirectToAction("StudDashboard", "Student");

                    case "parent":
                        // Verify parent record exists, create if missing
                        var parent = await _context.Parents
                            .FirstOrDefaultAsync(p => p.UserId == user.UserId);

                        if (parent == null)
                        {
                            // Auto-create missing Parent record
                            parent = new Parent
                            {
                                ParentId = user.UserId,
                                UserId = user.UserId
                            };
                            _context.Parents.Add(parent);
                            await _context.SaveChangesAsync();
                        }

                        // Set authentication session/cookie
                        _helper.SignIn(user.Email, "Parent", false);
                        return RedirectToAction("Dashboard", "Parent");

                    default:
                        ViewBag.ErrorMessage = "Invalid user type. Please contact administrator.";
                        return View();
                }
            }
            catch (Exception ex)
            {
                // Log the error in production
                ViewBag.ErrorMessage = $"An error occurred during login: {ex.Message}";
                return View();
            }
        }

        // POST: /Account/Logout
        public IActionResult Logout()
        {
            _helper.SignOut();
            return RedirectToAction("Login");
        }
    }
}
