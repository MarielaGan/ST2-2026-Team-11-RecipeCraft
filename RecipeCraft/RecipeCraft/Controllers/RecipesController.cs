using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RecipeCraft.Data;
using RecipeCraft.Models;

namespace RecipeCraft.Controllers
{
    public class RecipesController : Controller
    {
        private readonly ApplicationDbContext _context;

        // Впръскване на DbContext чрез Dependency Injection
        public RecipesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // 1. SELECT WITH FILTER (Търсене и филтриране)
        // ==========================================
        // Достъпно за ВСИЧКИ (включително нерегистрирани)
        [AllowAnonymous]
        public async Task<IActionResult> Index(string? searchString, string? category)
        {
            // Използваме Include(r => r.Category), за да вземем свързаните данни за категорията
            var query = _context.Recipes.Include(r => r.Category).AsQueryable();

            // Търсене по ключова дума в името или съставките
            if (!string.IsNullOrWhiteSpace(searchString))
            {
                query = query.Where(r => r.Title.Contains(searchString) || r.Ingredients.Contains(searchString));
            }

            // Филтриране по категория
            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(r => r.Category != null && r.Category.Name == category);
            }

            // Зареждаме категориите за падащото меню за филтриране
            ViewBag.Categories = await _context.Categories.ToListAsync();

            // Запазваме избраните филтри, за да се показват във формата във View-то
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentCategory"] = category;

            // Връщаме списъка с намерените рецепти
            return View(await query.ToListAsync());
        }

        // ==========================================
        // 2. INSERT (Добавяне на рецепта)
        // ==========================================
        // Изисква потребителят да е ВЛЯЗЪЛ в профила си
        [Authorize]
        public async Task<IActionResult> Create()
        {
            // Зареждаме категориите за падащото меню в Create View
            var categories = await _context.Categories.ToListAsync();
            ViewBag.CategoryId = new SelectList(categories, "Id", "Name");

            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Recipe recipe)
        {
            if (ModelState.IsValid)
            {
                _context.Recipes.Add(recipe);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            // При грешка във формата презареждаме категориите
            var categories = await _context.Categories.ToListAsync();
            ViewBag.CategoryId = new SelectList(categories, "Id", "Name", recipe.CategoryId);

            return View(recipe);
        }

        // ==========================================
        // 3. UPDATE (Редакция на рецепта)
        // ==========================================
        // Изисква потребителят да е ВЛЯЗЪЛ в профила си
        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var recipe = await _context.Recipes.FindAsync(id);
            if (recipe == null)
            {
                return NotFound();
            }

            // Зареждаме категориите за падащото меню при редактиране
            var categories = await _context.Categories.ToListAsync();
            ViewBag.CategoryId = new SelectList(categories, "Id", "Name", recipe.CategoryId);

            return View(recipe);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Recipe recipe)
        {
            if (id != recipe.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(recipe);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.Recipes.AnyAsync(r => r.Id == id))
                    {
                        return NotFound();
                    }
                    throw;
                }

                return RedirectToAction(nameof(Index));
            }

            // При грешка във формата презареждаме категориите
            var categories = await _context.Categories.ToListAsync();
            ViewBag.CategoryId = new SelectList(categories, "Id", "Name", recipe.CategoryId);

            return View(recipe);
        }

        // ==========================================
        // 4. DELETE (Изтриване на рецепта)
        // ==========================================
        // Достъпно САМО за Администратори (Role = Admin)
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var recipe = await _context.Recipes
                .Include(r => r.Category)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (recipe == null)
            {
                return NotFound();
            }

            return View(recipe);
        }

        [HttpPost, ActionName("Delete")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var recipe = await _context.Recipes.FindAsync(id);
            if (recipe != null)
            {
                _context.Recipes.Remove(recipe);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}