using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RecipeCraft.Configuration;
using RecipeCraft.Data;
using RecipeCraft.Models;
using RecipeCraft.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// 1. Регистрация на базата данни SQLite
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite("Data Source=recipecraft.db"));

// repository pattern
builder.Services.AddScoped<IDatabase, Database>();

// 2. Регистрация на Singleton за AppSettings
builder.Services.AddSingleton(AppSettingsManager.Instance);

// 3. Регистрация на ExternalApis опциите (чете от appsettings.json и appsettings.Development.json)
builder.Services.Configure<ExternalApisOptions>(
    builder.Configuration.GetSection(ExternalApisOptions.SectionName));

// 4. Регистрация на Gemini HTTP услугата (IGeminiQueryService)
builder.Services.AddHttpClient<IGeminiQueryService, GeminiQueryService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// 5. Добавяне на Cookie Authentication (С коригирани пътища към UsersController)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Users/Login";
        options.LogoutPath = "/Users/Logout";
        options.AccessDeniedPath = "/Users/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    });

var app = builder.Build();

// 6. АВТОМАТИЧНО СЪЗДАВАНЕ НА БАЗАТА ДАННИ И АДМИН ПОТРЕБИТЕЛ
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.EnsureCreated();

    if (!context.Users.Any(u => u.Role == "Admin"))
    {
        var hasher = new PasswordHasher<User>();
        var adminUser = new User
        {
            Username = "admin",
            FirstName = "Главен",
            LastName = "Администратор",
            Email = "admin@recipecraft.com",
            Role = "Admin"
        };
        adminUser.PasswordHash = hasher.HashPassword(adminUser, "admin123");
        context.Users.Add(adminUser);
        context.SaveChanges();
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Задължителен ред на middleware за автентикация
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();