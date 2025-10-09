using Microsoft.EntityFrameworkCore;
using Homecare.DAL;                    // ItemDbContext + DBInit
using Homecare.DAL.Interfaces;         // IRepository arayüzleri
using Homecare.DAL.Repositories;       // Repository sınıfları
using System.IO;
using Homecare.Models;

var builder = WebApplication.CreateBuilder(args);

// ---- MVC ----
builder.Services.AddControllersWithViews();

// ---- SQLite dosya yolu (App_Data/homecare.db) ----
var dataDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(dataDir);
var defaultSqlite = $"Data Source={Path.Combine(dataDir, "homecare.db")}";

// appsettings.Development.json içindeki bağlantı dizesi yoksa defaultSqlite kullan
var conn = builder.Configuration.GetConnectionString("AppDbContextConnection") ?? defaultSqlite;
Console.WriteLine($"[Homecare] SQLite -> {conn}");

// ---- DbContext ----
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite(conn);
});

// ---- Repositories (Scoped) ----
builder.Services.AddScoped<IAvailableSlotRepository, AvailableSlotRepository>();
builder.Services.AddScoped<IAppointmentRepository, AppointmentRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ICareTaskRepository, CareTaskRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    // DBInit.Seed içinde EnsureCreated() olmalı (SQLite için yeterli)
    DBInit.Seed(app);
}

app.UseStaticFiles();
app.UseAuthentication();

// Basit default route
app.MapDefaultControllerRoute();
// İstersen aşağıdaki klasik kalıbı da kullanabilirsin:
// app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
