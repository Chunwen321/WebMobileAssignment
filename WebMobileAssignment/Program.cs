var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();
app.UseHttpsRedirection();
app.UseStaticFiles();  
app.MapDefaultControllerRoute();
app.Run();

app.MapGet("/", () => "Hello chunwen123!");

app.Run();
