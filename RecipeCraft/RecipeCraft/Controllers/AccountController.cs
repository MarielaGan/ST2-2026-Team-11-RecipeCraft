using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using RecipeCraft.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace RecipeCraft.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Account/Login
        public IActionResult Login() => View();

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password)
        {
            // Намираме потребителя по име и парола
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == username && u.PasswordHash == password);

            if (user == null)
            {
                ViewBag.Error = "Невалидно потребителско име или парола!";
                return View();
            }

            // Създаваме "Claims" (информация за влезлия потребител)
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role) // Ролята (Admin или User)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            // Вписваме потребителя в сесията (Cookie)
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity));

            return RedirectToAction("Index", "Recipes");
        }

        // GET & POST: /Account/Logout
        [HttpGet, HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home"); // Пренасочва към началната страница с форма за вход
        }

        public IActionResult AccessDenied() => View();
    }
}
