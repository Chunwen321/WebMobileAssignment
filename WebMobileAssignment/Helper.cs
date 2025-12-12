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
     <div style='background-color: #f8d7da; border-left: 4px solid #dc3545; padding: 15px; margin: 20px 0'>
           <p style='margin: 0; color: #721c24'><strong>🔒 Security Reminder:</strong></p>
   <p style='margin: 5px 0 0 0; color: #721c24'>If you did not request this password reset, please contact the administrator immediately at {cf["Smtp:User"]}.</p>
    </div>
        <hr style='margin: 30px 0; border: none; border-top: 1px solid #dee2e6'>
          <p style='color: #6c757d; font-size: 12px; margin: 0'>
         This is an automated email from the Tuition Attendance System. Please do not reply to this email.
                </p>
   <p style='color: #6c757d; font-size: 12px; margin: 5px 0 0 0'>
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
}
