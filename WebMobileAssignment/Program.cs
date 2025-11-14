var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

<<<<<<< Updated upstream
app.MapGet("/", () => "Hello Chunwen!");
=======
app.MapGet("/", () => "Hello chunwen!");
>>>>>>> Stashed changes

app.Run();
