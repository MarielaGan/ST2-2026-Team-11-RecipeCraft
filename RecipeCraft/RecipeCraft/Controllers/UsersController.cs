using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeCraft.Data;
using RecipeCraft.Models;

namespace RecipeCraft.Controllers
{
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? searchString, string? roleFilter)
        {
            // Запазваме текущите стойности в ViewData, за да останат попълнени във формата след търсене
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentRole"] = roleFilter;

            var users = _context.Users.AsQueryable();

            // 1. Търсене по дума (в Потребителско име, Първо име, Фамилия или Имейл)
            if (!string.IsNullOrEmpty(searchString))
            {
                searchString = searchString.ToLower();
                users = users.Where(u => u.Username.ToLower().Contains(searchString)
                                      || u.FirstName.ToLower().Contains(searchString)
                                      || u.LastName.ToLower().Contains(searchString)
                                      || u.Email.ToLower().Contains(searchString));
            }

            // 2. Филтриране по роля (Admin / User)
            if (!string.IsNullOrEmpty(roleFilter))
            {
                users = users.Where(u => u.Role == roleFilter);
            }

            return View(await users.ToListAsync());
        }


        // ==========================================
        // 2. DETAILS (Детайли за потребител)
        // ==========================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(m => m.Id == id);

            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // ==========================================
        // 3. CREATE (Добавяне / Регистрация)
        // ==========================================

        // GET: Users/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(User user)
        {
            // Проверка за дублиращо се потребителско име или имейл
            if (await _context.Users.AnyAsync(u => u.Username == user.Username))
            {
                ModelState.AddModelError("Username", "Потребителското име вече е заето.");
            }

            if (await _context.Users.AnyAsync(u => u.Email == user.Email))
            {
                ModelState.AddModelError("Email", "Имейл адресът вече е регистриран.");
            }

            if (ModelState.IsValid)
            {
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(user);
        }

      
        // ==========================================
        // 4. EDIT (Редакция на потребител)
        // ==========================================

        // GET: Users/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            return View(user);
        }

        // POST: Users/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, User user)
        {
            if (id != user.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(user);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Users.Any(e => e.Id == user.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(user);
        }

        // ==========================================
        // 5. DELETE (Изтриване на потребител)
        // ==========================================

        // GET: Users/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(m => m.Id == id);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // POST: Users/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
