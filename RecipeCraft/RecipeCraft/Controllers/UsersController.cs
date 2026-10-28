using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeCraft.Data;
using RecipeCraft.Models;

namespace RecipeCraft.Controllers
{
    public class UsersController : Controller
    {
        private readonly IDatabase _database;

        public UsersController(IDatabase database)
        {
            _database = database;
        }

        // ==========================================
        // 1. INDEX (Списък с потребители + Филтриране за Админ)
        // ==========================================
        public async Task<IActionResult> Index(string? searchString, string? roleFilter)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentRole"] = roleFilter;

            var users = await _database.GetFilteredUsersAsync(searchString, roleFilter);
            return View(users);
        }

        // ==========================================
        // 2. LOGIN (Вход в системата)
        // ==========================================
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _database.GetUserByUsernameAsync(model.Username);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Грешно потребителско име или парола.");
                return View(model);
            }

            var hasher = new PasswordHasher<User>();
            var result = hasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);

            if (result == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(string.Empty, "Грешно потребителско име или парола.");
                return View(model);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role ?? "User")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity));

            return RedirectToAction("Index", "Home");
        }

        // ==========================================
        // 3. REGISTER (Регистрация)
        // ==========================================
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (await _database.UserExistsByUsernameAsync(model.Username))
            {
                ModelState.AddModelError("Username", "Потребителското име вече е заето.");
            }

            if (await _database.UserExistsByEmailAsync(model.Email))
            {
                ModelState.AddModelError("Email", "Имейл адресът вече се използва.");
            }

            if (ModelState.IsValid)
            {
                var user = new User
                {
                    Username = model.Username,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Email = model.Email,
                    Role = "User"
                };

                var hasher = new PasswordHasher<User>();
                user.PasswordHash = hasher.HashPassword(user, model.Password);

                await _database.AddUserAsync(user);

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Role, user.Role)
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity));

                return RedirectToAction("Index", "Home");
            }

            return View(model);
        }

        // ==========================================
        // 4. LOGOUT (Изход)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        // ==========================================
        // 5. MY PROFILE (Управление на личен профил)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return RedirectToAction(nameof(Login));

            int userId = int.Parse(userIdClaim);
            var user = await _database.GetUserByIdAsync(userId);

            if (user == null) return NotFound();

            return View("Details", user);
        }

        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return RedirectToAction(nameof(Login));

            int userId = int.Parse(userIdClaim);
            var user = await _database.GetUserByIdAsync(userId);

            if (user == null) return NotFound();

            return View("Edit", user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(User updatedUser)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return RedirectToAction(nameof(Login));

            int userId = int.Parse(userIdClaim);
            if (updatedUser.Id != userId) return Unauthorized();

            var existingUser = await _database.GetUserByIdAsync(userId);
            if (existingUser == null) return NotFound();

            if (await _database.UserExistsByUsernameAsync(updatedUser.Username, userId))
            {
                ModelState.AddModelError("Username", "Потребителското име вече е заето.");
            }

            if (await _database.UserExistsByEmailAsync(updatedUser.Email, userId))
            {
                ModelState.AddModelError("Email", "Имейл адресът вече е регистриран.");
            }

            if (ModelState.IsValid)
            {
                existingUser.Username = updatedUser.Username;
                existingUser.FirstName = updatedUser.FirstName;
                existingUser.LastName = updatedUser.LastName;
                existingUser.Email = updatedUser.Email;

                await _database.UpdateUserAsync(existingUser);

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, existingUser.Id.ToString()),
                    new Claim(ClaimTypes.Name, existingUser.Username),
                    new Claim(ClaimTypes.Role, existingUser.Role ?? "User")
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

                TempData["SuccessMessage"] = "Профилът е обновен успешно!";
                return RedirectToAction(nameof(Profile));
            }

            return View("Edit", updatedUser);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProfile()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return RedirectToAction(nameof(Login));

            int userId = int.Parse(userIdClaim);
            var user = await _database.GetUserByIdAsync(userId);

            if (user != null)
            {
                await _database.DeleteUserAsync(userId);
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }

            return RedirectToAction("Index", "Home");
        }

        // ==========================================
        // 6. ADMINISTRATIVE CRUD (Details, Create, Edit, Delete)
        // ==========================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var user = await _database.GetUserByIdAsync(id.Value);
            if (user == null) return NotFound();
            return View(user);
        }

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(User user)
        {
            if (await _database.UserExistsByUsernameAsync(user.Username))
                ModelState.AddModelError("Username", "Потребителското име вече е заето.");

            if (await _database.UserExistsByEmailAsync(user.Email))
                ModelState.AddModelError("Email", "Имейл адресът вече е регистриран.");

            if (ModelState.IsValid)
            {
                var hasher = new PasswordHasher<User>();
                user.PasswordHash = hasher.HashPassword(user, user.PasswordHash);
                await _database.AddUserAsync(user);
                return RedirectToAction(nameof(Index));
            }
            return View(user);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var user = await _database.GetUserByIdAsync(id.Value);
            if (user == null) return NotFound();
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, User user)
        {
            if (id != user.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    await _database.UpdateUserAsync(user);
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _database.UserExistsByIdAsync(user.Id)) return NotFound();
                    else throw;
                }

                // Проверяваме дали текущо влезлият потребител редактира СОБСТВЕНИЯ си профил
                var currentUserIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (currentUserIdClaim != null && int.Parse(currentUserIdClaim) == user.Id)
                {
                    TempData["SuccessMessage"] = "Профилът е обновен успешно!";
                    return RedirectToAction(nameof(Profile)); // Връща го в личния му профил
                }

                // Ако Администратор редактира друг потребител -> отива в списъка (Index)
                return RedirectToAction(nameof(Index));
            }
            return View(user);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var user = await _database.GetUserByIdAsync(id.Value);
            if (user == null) return NotFound();
            return View(user);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _database.DeleteUserAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }
}