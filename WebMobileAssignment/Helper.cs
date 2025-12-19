using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace WebMobileAssignment;

public class Helper(IWebHostEnvironment en,
                    IHttpContextAccessor ct,
                    IConfiguration cf)
{
    // ------------------------------------------------------------------------
    // Photo Upload
    // ------------------------------------------------------------------------

    public string ValidatePhoto(IFormFile f)
    {
        var reType = new Regex(@"^image\/(jpeg|png)$", RegexOptions.IgnoreCase);
        var reName = new Regex(@"^.+\.(jpeg|jpg|png)$", RegexOptions.IgnoreCase);

        if (!reType.IsMatch(f.ContentType) || !reName.IsMatch(f.FileName))
        {
            return "Only JPG and PNG photo is allowed.";
        }
        else if (f.Length > 1 * 1024 * 1024)
        {
            return "Photo size cannot more than 1MB.";
        }

        return "";
    }

    public string SavePhoto(IFormFile f, string folder)
    {
        var file = Guid.NewGuid().ToString("n") + ".jpg";
        var path = Path.Combine(en.WebRootPath, folder, file);

        var options = new ResizeOptions
        {
            Size = new(200, 200),
            Mode = ResizeMode.Crop,
        };

        using var stream = f.OpenReadStream();
        //using var img = Image.Load(stream);
        //img.Mutate(x => x.Resize(options));
        //img.Save(path);

        return file;
    }

    public void DeletePhoto(string file, string folder)
    {
        file = Path.GetFileName(file);
        var path = Path.Combine(en.WebRootPath, folder, file);
        File.Delete(path);
    }



    // ------------------------------------------------------------------------
    // Security Helper Functions
    // ------------------------------------------------------------------------

    private readonly PasswordHasher<object> ph = new();

    public string HashPassword(string password)
    {
        return ph.HashPassword(0, password);
    }

    public bool VerifyPassword(string hash, string password)
    {
        return ph.VerifyHashedPassword(0, hash, password)
               == PasswordVerificationResult.Success;
    }

    public void SignIn(string email, string role, bool rememberMe)
    {
        List<Claim> claims =
        [
            new(ClaimTypes.Name, email),
            new(ClaimTypes.Role, role),
        ];

        ClaimsIdentity identity = new(claims, "Cookies");

        ClaimsPrincipal principal = new(identity);

        AuthenticationProperties properties = new()
        {
            IsPersistent = rememberMe,
        };

        ct.HttpContext!.SignInAsync(principal, properties);
    }

    public void SignOut()
    {
        ct.HttpContext!.SignOutAsync();
    }

    public string RandomPassword()
    {
        string s = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        string password = "";

        Random r = new();

        for (int i = 1; i <= 10; i++)
        {
            password += s[r.Next(s.Length)];
        }

        return password;
    }

    /// <summary>
    /// Validates password against strong password policy requirements.
    /// </summary>
    /// <param name="password">The password to validate</param>
    /// <returns>Validation result with success flag and error messages</returns>
    public (bool IsValid, List<string> Errors) ValidatePasswordStrength(string password)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(password))
        {
            errors.Add("Password is required.");
            return (false, errors);
        }

        // Minimum length requirement
        if (password.Length < 8)
        {
            errors.Add("Password must be at least 8 characters long.");
        }

        // Maximum length for security
        if (password.Length > 128)
        {
            errors.Add("Password cannot exceed 128 characters.");
        }

        // Uppercase letter requirement
        if (!Regex.IsMatch(password, @"[A-Z]"))
        {
            errors.Add("Password must contain at least one uppercase letter (A-Z).");
        }

        // Lowercase letter requirement
        if (!Regex.IsMatch(password, @"[a-z]"))
        {
            errors.Add("Password must contain at least one lowercase letter (a-z).");
        }

        // Digit requirement
        if (!Regex.IsMatch(password, @"[0-9]"))
        {
            errors.Add("Password must contain at least one digit (0-9).");
        }

        // Special character requirement
        if (!Regex.IsMatch(password, @"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>/?]"))
        {
            errors.Add("Password must contain at least one special character (!@#$%^&* etc.).");
        }

        // Check for common weak passwords
        var commonPasswords = new[] { "password", "12345678", "password123", "qwerty", "abc123" };
        if (commonPasswords.Any(cp => password.ToLower().Contains(cp)))
        {
            errors.Add("Password contains common patterns that are not secure.");
        }

        return (errors.Count == 0, errors);
    }



    // ------------------------------------------------------------------------
    // Email Helper Functions
    // ------------------------------------------------------------------------

    public void SendEmail(MailMessage mail)
    {
        string user = cf["Smtp:User"] ?? "";
        string pass = cf["Smtp:Pass"] ?? "";
        string name = cf["Smtp:Name"] ?? "";
        string host = cf["Smtp:Host"] ?? "";
        int port = cf.GetValue<int>("Smtp:Port");

        mail.From = new MailAddress(user, name);

        using var smtp = new SmtpClient
        {
            Host = host,
            Port = port,
            EnableSsl = true,
            Credentials = new NetworkCredential(user, pass),
        };

        smtp.Send(mail);

        Console.WriteLine($"{user} {pass} {name} {host} {port}");
    }

    public void SendPasswordResetEmail(string toEmail, string userName, string temporaryPassword)
    {
        var mailMessage = new MailMessage
        {
            To = { toEmail },
            Subject = "Password Reset - Tuition Attendance System",
            Body = $@"
<html>
  <body style='font-family: Arial, sans-serif;'>
  <div style='max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f8f9fa;'>
       <div style='background-color: white; padding: 30px; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1)'>
     <h2 style='color: #495057; margin-bottom: 20px;'>Password Reset Request</h2>
      <p>Hello <strong>{userName}</strong>,</p>
       <p>You have requested to reset your password for the Tuition Attendance System.</p>
                <p style='margin-top: 20px;'>Your temporary password is:</p>
  <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 20px; border-radius: 8px; margin: 20px 0; text-align: center'>
       <h2 style='margin: 0; color: white; letter-spacing: 3px; font-family: monospace;'>{temporaryPassword}</h2>
          </div>
          <div style='background-color: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; margin: 20px 0'>
         <p style='margin: 0; color: #856404'><strong>⚠️ Important Security Notice:</strong></p>
        <p style='margin: 5px 0 0 0; color: #856404'>This is a temporary password. Please change it immediately after logging in.</p>
      </div>
        <h3 style='color: #495057; margin-top: 30px'>Next Steps:</h3>
                <ol style='line-height: 1.8'>
          <li>Log in to your account using the temporary password above</li>
        <li>Navigate to your account settings or change password page</li>
             <li>Create a new secure password</li>
         <li>Keep your new password safe and secure</li>
    </ol>
     <div style='background-color: #f8d7da; border-left: 4px solid #dc3545; padding: 15px, margin: 20px 0'>
           <p style='margin: 0; color: #721c24'><strong>🔒 Security Reminder:</strong></p>
   <p style='margin: 5px 0 0 0; color: #721c24'>If you did not request this password reset, please contact the administrator immediately at {cf["Smtp:User"]}.</p>
    </div>
        <hr style='margin: 30px 0; border: none; border-top: 1px solid #dee2e6'>
          <p style='color: #6c757d; font-size: 12px; margin: 0'>
         This is an automated email from the Tuition Attendance System. Please do not reply to this email.
                </p>
   <p style='color: #6c757d; font-size: 12px, margin: 5px 0 0 0'>
          © {DateTime.Now.Year} Tuition Attendance System. All rights reserved.
                </p>
            </div>
 </div>
    </body>
</html>",
                IsBodyHtml = true
        };

        SendEmail(mailMessage);
    }

    public void SendPasswordChangeConfirmationEmail(string toEmail, string userName)
    {
        var baseUrl = cf["AppBaseUrl"];
        
        if (!string.IsNullOrEmpty(baseUrl))
        {
            Console.WriteLine($"✅ Using configured AppBaseUrl: {baseUrl}");
        }
        
        // If not in config, try to get from HTTP context
        if (string.IsNullOrEmpty(baseUrl) && ct.HttpContext != null)
        {
            var request = ct.HttpContext.Request;
            baseUrl = $"{request.Scheme}://{request.Host}";
            Console.WriteLine($"✅ Auto-detected URL from HTTP context: {baseUrl}");
        }
        
        // Fallback to localhost with HTTPS (port 7106 is default for .NET HTTPS)
        if (string.IsNullOrEmpty(baseUrl))
        {
            baseUrl = "https://localhost:7106";
            Console.WriteLine($"⚠️ Using fallback URL: {baseUrl}");
        }
        
        var loginUrl = $"{baseUrl}/Account/Login";
        
        var mailMessage = new MailMessage
        {
            To = { toEmail },
            Subject = "Password Changed - Tuition Attendance System",
            Body = $@"
<html>
  <body style='font-family: Arial, sans-serif;'>
    <div style='max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f8f9fa;'>
      <div style='background-color: white; padding: 30px; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1)'>
        <h2 style='color: #495057; margin-bottom: 20px;'>Password Changed Successfully</h2>
        <p>Hello <strong>{userName}</strong>,</p>
        <p>Your password has been successfully changed for the Tuition Attendance System.</p>
        
        <div style='background-color: #d1ecf1; border-left: 4px solid #0c5460; padding: 15px; margin: 20px 0'>
          <p style='margin: 0; color: #0c5460'><strong>✓ Confirmation:</strong></p>
          <p style='margin: 5px 0 0 0; color: #0c5460'>Your password was updated on {DateTime.Now:MMMM dd, yyyy} at {DateTime.Now:hh:mm tt}.</p>
        </div>

        <div style='background-color: #f8d7da; border-left: 4px solid #dc3545; padding: 15px; margin: 20px 0'>
          <p style='margin: 0; color: #721c24'><strong>🔒 Security Notice:</strong></p>
          <p style='margin: 5px 0 0 0; color: #721c24'>If you did not make this change, please contact the administrator immediately at {cf["Smtp:User"]}.</p>
        </div>

        <div style='text-align: center; margin-top: 30px; padding-top: 20px; border-top: 1px solid #dee2e6;'>
          <a href='{loginUrl}' style='display: inline-block; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 12px 30px; border-radius: 6px; text-decoration: none; font-weight: 600; font-size: 14px;'>
            Login Now
          </a>
        </div>

        <hr style='margin: 30px 0; border: none; border-top: 1px solid #dee2e6'>
        <p style='color: #6c757d; font-size: 12px; margin: 0; text-align: center;'>
          This is an automated email from the Tuition Attendance System. Please do not reply to this email.
        </p>
        <p style='color: #6c757d; font-size: 12px; margin: 5px 0 0 0; text-align: center;'>
          © {DateTime.Now.Year} Tuition Attendance System. All rights reserved.
        </p>
      </div>
    </div>
  </body>
</html>",
            IsBodyHtml = true
        };

        SendEmail(mailMessage);
    }

    public void SendWelcomeEmail(string toEmail, string userName, string userType, string defaultPassword)
    {
        // Try to get base URL from configuration, or construct from HTTP context
        var baseUrl = cf["AppBaseUrl"];
        
        if (!string.IsNullOrEmpty(baseUrl))
        {
            Console.WriteLine($"✅ Using configured AppBaseUrl: {baseUrl}");
        }
        
        // If not in config, try to get from HTTP context
        if (string.IsNullOrEmpty(baseUrl) && ct.HttpContext != null)
        {
            var request = ct.HttpContext.Request;
            baseUrl = $"{request.Scheme}://{request.Host}";
            Console.WriteLine($"✅ Auto-detected URL from HTTP context: {baseUrl}");
        }
        
        // Fallback to localhost with HTTPS (port 7106 is default for .NET HTTPS)
        if (string.IsNullOrEmpty(baseUrl))
        {
            baseUrl = "https://localhost:7106";
            Console.WriteLine($"⚠️ Using fallback URL: {baseUrl}");
        }
        
        var resetPasswordUrl = $"{baseUrl}/Account/SetNewPassword";
        var loginUrl = $"{baseUrl}/Account/Login";
        
        var mailMessage = new MailMessage
        {
            To = { toEmail },
            Subject = $"Welcome to Tuition Attendance System - {userType} Account Created",
            Body = $@"
<html>
  <body style='font-family: Arial, sans-serif;'>
    <div style='max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f8f9fa;'>
      <div style='background-color: white; padding: 30px; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1)'>
        <div style='text-align: center; margin-bottom: 30px;'>
          <h1 style='color: #495057; margin-bottom: 10px;'>Welcome to Tuition Attendance System!</h1>
          <p style='color: #6c757d; font-size: 14px; margin: 0;'>Your {userType} account has been successfully created</p>
        </div>
        
        <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 20px; border-radius: 8px; margin: 20px 0;'>
          <p style='color: white; margin: 0 0 10px 0; font-size: 14px;'>Hello <strong>{userName}</strong>,</p>
          <p style='color: white; margin: 0; font-size: 14px;'>Your account credentials have been set up. Please find your login information below.</p>
        </div>

        <div style='background-color: #f8f9fa; padding: 20px; border-radius: 8px; margin: 20px 0;'>
          <h3 style='color: #495057; margin: 0 0 15px 0; font-size: 16px;'>Login Credentials</h3>
          <table style='width: 100%; border-collapse: collapse;'>
            <tr>
              <td style='padding: 8px 0; color: #6c757d; font-size: 14px;'><strong>Email:</strong></td>
              <td style='padding: 8px 0; color: #495057; font-size: 14px;'>{toEmail}</td>
            </tr>
            <tr>
              <td style='padding: 8px 0; color: #6c757d; font-size: 14px;'><strong>Temporary Password:</strong></td>
              <td style='padding: 8px 0;'>
                <code style='background-color: #e9ecef; padding: 5px 10px; border-radius: 4px; font-family: monospace; color: #dc3545; font-size: 14px;'>{defaultPassword}</code>
              </td>
            </tr>
          </table>
        </div>

        <div style='background-color: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; margin: 20px 0;'>
          <p style='margin: 0; color: #856404; font-size: 14px;'><strong>⚠️ Important Security Notice:</strong></p>
          <p style='margin: 5px 0 0 0; color: #856404; font-size: 13px;'>For your security, we strongly recommend changing your password immediately after your first login.</p>
        </div>

        <h3 style='color: #495057; margin-top: 30px; font-size: 16px;'>How to Get Started:</h3>
        <ol style='line-height: 1.8; color: #495057; font-size: 14px;'>
          <li>Visit the login page: <a href='{loginUrl}' style='color: #667eea; text-decoration: none;'>{loginUrl}</a></li>
          <li>Log in using your email and the temporary password above</li>
          <li>Navigate to your profile settings to change your password</li>
          <li>Or use the Forgot Password link below to set a new password immediately</li>
        </ol>

        <div style='text-align: center; margin-top: 30px; padding-top: 20px; border-top: 1px solid #dee2e6;'>
          <a href='{resetPasswordUrl}' style='display: inline-block; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 12px 30px; border-radius: 6px; text-decoration: none; font-weight: 600; font-size: 14px;'>
            Reset Password Now
          </a>
        </div>

        <div style='background-color: #f8d7da; border-left: 4px solid #dc3545; padding: 15px, margin: 30px 0 20px 0'>
          <p style='margin: 0; color: #721c24; font-size: 13px;'><strong>🔒 Security Reminder:</strong></p>
          <p style='margin: 5px 0 0 0; color: #721c24; font-size: 12px;'>Never share your password with anyone. If you did not expect this email, please contact the administrator immediately at {cf["Smtp:User"]}.</p>
        </div>

        <hr style='margin: 30px 0; border: none; border-top: 1px solid #dee2e6;'>
        <p style='color: #6c757d; font-size: 12px; margin: 0; text-align: center;'>
          This is an automated email from the Tuition Attendance System. Please do not reply to this email.
        </p>
        <p style='color: #6c757d; font-size: 12px; margin: 5px 0 0 0; text-align: center;'>
          © {DateTime.Now.Year} Tuition Attendance System. All rights reserved.
        </p>
      </div>
    </div>
  </body>
</html>",
            IsBodyHtml = true
        };

        SendEmail(mailMessage);
    }

    public void SendLeaveApprovalEmail(string toEmail, string userName, DateTime startDate, DateTime endDate, int totalDays, string reason, string? remarks)
    {
        var mailMessage = new MailMessage
        {
            To = { toEmail },
            Subject = "Leave Application Approved - Tuition Attendance System",
            Body = $@"
                <html>
                  <body style='font-family: Arial, sans-serif;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f8f9fa;'>
                      <div style='background-color: white; padding: 30px; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1)'>
                        <h2 style='color: #28a745;'>Leave Application Approved</h2>
                        <p>Dear <strong>{userName}</strong>,</p>
                        <p>Your leave application has been <strong style='color: #28a745;'>approved</strong>.</p>
                            
                        <div style='background-color: #f8f9fa; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                          <p style='margin: 5px 0;'><strong>Leave Period:</strong> {startDate:dd MMM yyyy} to {endDate:dd MMM yyyy}</p>
                          <p style='margin: 5px 0;'><strong>Total Days:</strong> {totalDays} day(s)</p>
                          <p style='margin: 5px 0;'><strong>Reason:</strong> {reason}</p>
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
        SendEmail(mailMessage);
    }

    public void SendLeaveRejectionEmail(string toEmail, string userName, DateTime startDate, DateTime endDate, int totalDays, string reason, string? remarks)
    {
        var mailMessage = new MailMessage
        {
            To = { toEmail },
            Subject = "Leave Application Rejected - Tuition Attendance System",
            Body = $@"
                <html>
                  <body style='font-family: Arial, sans-serif;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f8f9fa;'>
                      <div style='background-color: white; padding: 30px; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1)'>
                        <h2 style='color: #dc3545;'>Leave Application Rejected</h2>
                        <p>Dear <strong>{userName}</strong>,</p>
                        <p>We regret to inform you that your leave application has been <strong style='color: #dc3545;'>rejected</strong>.</p>
                            
                        <div style='background-color: #f8f9fa; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                          <p style='margin: 5px 0;'><strong>Leave Period:</strong> {startDate:dd MMM yyyy} to {endDate:dd MMM yyyy}</p>
                          <p style='margin: 5px 0;'><strong>Total Days:</strong> {totalDays} day(s)</p>
                          <p style='margin: 5px 0;'><strong>Reason:</strong> {reason}</p>
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
        SendEmail(mailMessage);
    }
}
