using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebMobileAssignment.Models;

namespace WebMobileAssignment.Controllers
{
    /// <summary>
    /// Utility controller for password management and database utilities
    /// WARNING: Remove or secure this controller in production!
    /// </summary>
    public class UtilityController : Controller
    {
    private readonly DB _context;
        private readonly Helper _helper;

        public UtilityController(DB context, Helper helper)
        {
   _context = context;
            _helper = helper;
        }

        // GET: /Utility/ResetAdminPassword
        // This page allows you to reset admin password without logging in
        public IActionResult ResetAdminPassword()
        {
            return View();
     }

// POST: /Utility/ResetAdminPassword
        [HttpPost]
        public async Task<IActionResult> ResetAdminPassword(string email, string newPassword)
 {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(newPassword))
    {
          ViewBag.ErrorMessage = "Please enter both email and new password.";
      return View();
    }

            try
            {
              // Find user by email
      var user = await _context.Users
     .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

     if (user == null)
     {
  ViewBag.ErrorMessage = "User not found with that email.";
 return View();
      }

                // Hash the new password
     user.PasswordHash = _helper.HashPassword(newPassword);

            // Update in database
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

       ViewBag.SuccessMessage = $"Password updated successfully for {user.Email}!<br/>" +
       $"User Type: {user.UserType}<br/>" +
                $"You can now login with your new password.";
                return View();
            }
            catch (Exception ex)
            {
         ViewBag.ErrorMessage = $"Error updating password: {ex.Message}";
    return View();
         }
        }

   // GET: /Utility/HashPassword
        // Simple page to generate password hashes
        public IActionResult HashPassword()
    {
            return View();
    }

    // POST: /Utility/HashPassword
        [HttpPost]
 public IActionResult HashPassword(string password)
      {
   if (string.IsNullOrWhiteSpace(password))
        {
        ViewBag.ErrorMessage = "Please enter a password to hash.";
      return View();
        }

     var hashedPassword = _helper.HashPassword(password);
    ViewBag.PlainPassword = password;
        ViewBag.HashedPassword = hashedPassword;
          ViewBag.SuccessMessage = "Password hashed successfully! Copy the hashed value below and paste it into your database.";

       return View();
        }
    }
}
