using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using RecipeCraft.Data;
using RecipeCraft.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// 1. Регистрация на базата данни SQLite
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite("Data Source=recipecraft.db"));

// 2. Регистрация на Singleton за AppSettings
builder.Services.AddSingleton(AppSettingsManager.Instance);

// 1. Добавяме Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login"; // Къде да препраща, ако потребителят не е влязъл
        options.AccessDeniedPath = "/Account/AccessDenied"; // Къде да препраща при липса на права
    });

var app = builder.Build();

// 3. АВТОМАТИЧНО СЪЗДАВАНЕ НА БАЗАТА ДАННИ
// Този блок проверява дали файлът recipecraft.db съществува и ако го няма, го създава сам!
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    // Уверяваме се, че базата данни е създадена
    context.Database.EnsureCreated();

    // Ако няма нито един потребител с роля Admin, създаваме такъв
    if (!context.Users.Any(u => u.Role == "Admin"))
    {
        var adminUser = new RecipeCraft.Models.User
        {
            Username = "admin",
            FirstName = "Главен",
            LastName = "Администратор",
            Email = "admin@recipecraft.com",
            PasswordHash = "admin123", // Паролата за вход
            Role = "Admin"
        };

        context.Users.Add(adminUser);
        context.SaveChanges();
    }
}

app.Run();