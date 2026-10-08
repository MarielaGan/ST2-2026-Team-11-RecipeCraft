using Microsoft.EntityFrameworkCore;
using ST2_2026_Team_10_RecipeCraft.Data;
using ST2_2026_Team_10_RecipeCraft.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// 1. Регистрация на базата данни SQLite
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite("Data Source=recipecraft.db"));

// 2. Регистрация на Singleton за AppSettings
builder.Services.AddSingleton(AppSettingsManager.Instance);

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
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Recipes}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();