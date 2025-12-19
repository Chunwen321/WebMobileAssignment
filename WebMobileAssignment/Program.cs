using WebMobileAssignment;
using WebMobileAssignment.Models;
using WebMobileAssignment.Services;
using QuestPDF.Infrastructure;

// Configure QuestPDF License
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Add MVC services
builder.Services.AddControllersWithViews();

// Add Database Context (SQL Server LocalDB)
builder.Services.AddSqlServer<DB>($@"
    Data Source=(LocalDB)\MSSQLLocalDB;
    AttachDbFilename={builder.Environment.ContentRootPath}\DB.mdf;
");

// Add Helper service
builder.Services.AddScoped<Helper>();

// Add Report service
builder.Services.AddScoped<ReportService>();

// Add ReCaptcha service with HttpClient
builder.Services.AddHttpClient<ReCaptchaService>();

// Add S3 Service
builder.Services.AddSingleton<S3Service>();

// Add Authentication with Cookie
builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(24);
        options.SlidingExpiration = true;

        // Add event handler to force logout on access denied
        options.Events.OnRedirectToAccessDenied = async context =>
        {
            // Force sign out when access is denied
            await Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions.SignOutAsync(context.HttpContext);
            context.Response.Redirect("/Account/AccessDenied?returnUrl=" + context.Request.Path);
        };
    });

// Add HttpContextAccessor for accessing HttpContext in services
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Initialize ID counters from existing database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DB>();
    try
    {
        db.Database.EnsureCreated();
        IdGenerator.InitializeCounters(db);
    }
    catch
    {
        // Database not yet created, counters will start from 1
    }
}

// Configure error handling
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Configure static files with no-cache headers in development
if (app.Environment.IsDevelopment())
{
    app.UseStaticFiles(new StaticFileOptions
    {
        OnPrepareResponse = ctx =>
        {
            ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
            ctx.Context.Response.Headers.Append("Expires", "0");
        }
    });
}
else
{
    app.UseStaticFiles();
}

app.UseRouting();

// Add Authentication & Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
