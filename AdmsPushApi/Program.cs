using Microsoft.EntityFrameworkCore;
using AdmsPushApi.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=attendance.db"));

builder.Services.AddControllers();

var app = builder.Build();

// Create SQLite database automatically on startup if it doesn't exist
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.EnsureCreated();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

app.Run();
