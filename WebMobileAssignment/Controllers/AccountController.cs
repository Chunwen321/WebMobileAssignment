using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebMobileAssignment.Models;
using WebMobileAssignment.Services;

namespace WebMobileAssignment.Controllers
{
    public class AccountController : Controller
    {
        private readonly DB _context;
        private readonly Helper _helper;
        private readonly ReCaptchaService _reCaptchaService;
        private readonly IConfiguration _configuration;

        public AccountController(DB context, Helper helper, ReCaptchaService reCaptchaService, IConfiguration configuration)
        {
            _context = context;
            _helper = helper;
            _reCaptchaService = reCaptchaService;
            _configuration = configuration;
        }

        // GET: /Account/Login
        public IActionResult Login(string? returnUrl = null)
        {
            // If user is trying to access a protected page without being logged in,
            // redirect to Access Denied page
            if (!string.IsNullOrEmpty(returnUrl))
            {
                return RedirectToAction("AccessDenied", new { returnUrl = returnUrl });
            }

            ViewBag.ReCaptchaSiteKey = _configuration["ReCaptcha:SiteKey"];
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

        // GET: /Account/SetNewPassword
        public IActionResult SetNewPassword()
        {
            return View();
        }

        // POST: /Account/SetNewPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetNewPassword(string email, string newPassword, string confirmPassword)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(email))
            {
                ViewBag.ErrorMessage = "Please enter your email address.";
                return View();
            }

            if (string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
            {
                ViewBag.ErrorMessage = "Please enter both password fields.";
                return View();
            }

            // Verify passwords match
            if (newPassword != confirmPassword)
            {
                ViewBag.ErrorMessage = "Passwords do not match.";
                return View();
            }

            // Verify password length
            if (newPassword.Length < 8)
            {
                ViewBag.ErrorMessage = "Password must be at least 8 characters long.";
                return View();
            }

            try
            {
                // Find user by email
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

                // For security reasons, show generic message even if user not found
                if (user == null || !user.IsActive)
                {
                    ViewBag.ErrorMessage = "Unable to update password. Please verify your email address or contact support.";
                    return View();
                }

                // Hash and update new password
                user.PasswordHash = _helper.HashPassword(newPassword);
                await _context.SaveChangesAsync();

                ViewBag.SuccessMessage = "Password has been set successfully! You can now login with your new password.";
                
                // Optionally, send a confirmation email
                try
                {
                    _helper.SendPasswordChangeConfirmationEmail(email, user.FullName);
                }
                catch (Exception emailEx)
                {
                    Console.WriteLine($"Confirmation email failed: {emailEx.Message}");
                    // Don't show error to user since password was changed successfully
                }

                return View();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Set password error: {ex.Message}");
                ViewBag.ErrorMessage = "An error occurred while setting your password. Please try again later.";
                return View();
            }
        }

        // POST: /Account/Login
        [HttpPost]
        public async Task<IActionResult> Login(string email, string password, string? recaptchaToken, bool rememberMe = false)
        {
            Console.WriteLine($"\n=== LOGIN ATTEMPT STARTED ===");
            Console.WriteLine($"Email: {email}");
            Console.WriteLine($"Token received: {(string.IsNullOrEmpty(recaptchaToken) ? "NO" : "YES - Length: " + recaptchaToken.Length)}");
            
            ViewBag.ReCaptchaSiteKey = _configuration["ReCaptcha:SiteKey"];

          // Verify reCAPTCHA v2 only if it's configured
     var siteKey = _configuration["ReCaptcha:SiteKey"];
            Console.WriteLine($"Site Key configured: {!string.IsNullOrEmpty(siteKey)}");

            if (!string.IsNullOrEmpty(siteKey) && siteKey != "YOUR_SITE_KEY_HERE" && !string.IsNullOrEmpty(recaptchaToken))
            {
     Console.WriteLine($"Verifying reCAPTCHA v2 token...");
          var isRecaptchaValid = await _reCaptchaService.VerifyTokenAsync(recaptchaToken);

                if (!isRecaptchaValid)
     {
            Console.WriteLine($"reCAPTCHA v2 verification FAILED for: {email}");
 ViewBag.ErrorMessage = "Security verification failed. Please try again.";
          return View();
      }

          Console.WriteLine($"reCAPTCHA v2 verification PASSED for email: {email}");
    }
            else if (string.IsNullOrEmpty(recaptchaToken))
            {
      Console.WriteLine($"Warning: reCAPTCHA token is MISSING for login attempt: {email}");
   }
            else
   {
  Console.WriteLine($"reCAPTCHA skipped (not configured or invalid site key)");
      }

            Console.WriteLine($"=== LOGIN ATTEMPT CONTINUING ===\n");

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
            // For security: don't reveal that the email doesn't exist
            // Use generic error message without showing attempts
            Console.WriteLine($"Login failed: Email not found - {email}");
            ViewBag.ErrorMessage = "Invalid email or password.";
            // Clear the form by redirecting to a fresh login page
            ModelState.Clear();
           return View();
        }

   // Check if account is locked
             if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.Now)
       {
       var remainingTime = user.LockoutEnd.Value - DateTime.Now;
            var minutes = (int)Math.Ceiling(remainingTime.TotalMinutes);
        
       Console.WriteLine($"Account locked for {email}. Remaining time: {minutes} minutes");
        ViewBag.ErrorMessage = $"Your account has been locked due to multiple failed login attempts. Please try again in {minutes} minute(s).";
        // Keep the email but clear password
        ViewBag.Email = email;
  return View();
          }

     // Reset failed attempts if lockout period has expired
  if (user.LockoutEnd.HasValue && user.LockoutEnd.Value <= DateTime.Now)
 {
       user.FailedLoginAttempts = 0;
                user.LockoutEnd = null;
                  user.LastFailedLogin = null;
           await _context.SaveChangesAsync();
              Console.WriteLine($"Lockout expired for {email}. Reset failed attempts.");
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
          // Increment failed login attempts
  user.FailedLoginAttempts++;
           user.LastFailedLogin = DateTime.Now;

        // Lock account if 5 or more failed attempts
             if (user.FailedLoginAttempts >= 5)
     {
        user.LockoutEnd = DateTime.Now.AddMinutes(15); // Lock for 15 minutes
           await _context.SaveChangesAsync();
           
       Console.WriteLine($"Account locked for {email} after {user.FailedLoginAttempts} failed attempts");
    ViewBag.ErrorMessage = "Your account has been locked due to multiple failed login attempts. Please try again in 15 minutes.";
        // Keep the email but clear password
        ViewBag.Email = email;
 return View();
      }

    await _context.SaveChangesAsync();
 
          var remainingAttempts = 5 - user.FailedLoginAttempts;
  Console.WriteLine($"Failed login for {email}. Attempts: {user.FailedLoginAttempts}/5. Remaining: {remainingAttempts}");
                    
      ViewBag.ErrorMessage = $"Invalid email or password. You have {remainingAttempts} attempt(s) remaining before your account is locked.";
        // Keep the email but clear password
        ViewBag.Email = email;
     return View();
        }

                // Successful login - Reset failed attempts
  if (user.FailedLoginAttempts > 0)
        {
         Console.WriteLine($"Successful login for {email}. Resetting failed attempts (was: {user.FailedLoginAttempts})");
     user.FailedLoginAttempts = 0;
          user.LastFailedLogin = null;
          user.LockoutEnd = null;
           await _context.SaveChangesAsync();
    }

       // Check if user is active
        if (!user.IsActive || user.Status != "active")
                {
    ViewBag.ErrorMessage = "Your account is not active. Please contact administrator.";
        return View();
         }

            // Redirect based on user type from Users table
    string userType = user.UserType.ToLower();

  switch (userType)
 {
            case "admin":
      _helper.SignIn(user.Email, "Admin", rememberMe);
     return RedirectToAction("Dashboard", "Admin");

               case "teacher":
  _helper.SignIn(user.Email, "Teacher", rememberMe);
           return RedirectToAction("TeachDashboard", "Teacher");

     case "student":
     _helper.SignIn(user.Email, "Student", rememberMe);
           return RedirectToAction("StudDashboard", "Student");

     case "parent":
          _helper.SignIn(user.Email, "Parent", rememberMe);
            return RedirectToAction("Dashboard", "Parent");

  default:
             ViewBag.ErrorMessage = "Invalid user type. Please contact administrator.";
        return View();
           }
            }
            catch (Exception ex)
          {
         // Log the error in production
           Console.WriteLine($"Login error: {ex.Message}");
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

        // GET: /Account/AccessDenied
        public IActionResult AccessDenied(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            
            // Optional: Force logout if user tries to access unauthorized page
    // This prevents them from staying logged in but blocked
    _helper.SignOut();
        
        ViewBag.ErrorMessage = "Access Denied. You do not have permission to access this page.";
  return View();
        }
    }
}
