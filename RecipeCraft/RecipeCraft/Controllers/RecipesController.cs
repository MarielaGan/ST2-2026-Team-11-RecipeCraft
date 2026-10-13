using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeCraft.Data;
using RecipeCraft.Models;

namespace RecipeCraft.Controllers
{
    public class RecipesController:Controller
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
        public async Task<IActionResult> Index(string? searchString, string? category)
        {
            var query = _context.Recipes.AsQueryable();

            // Търсене по ключова дума в името или съставките
            if (!string.IsNullOrWhiteSpace(searchString))
            {
                query = query.Where(r => r.Title.Contains(searchString) || r.Ingredients.Contains(searchString));
            }

            // Филтриране по категория
            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(r => r.Category == category);
            }

            // Запазваме избраните филтри, за да се показват във формата във View-то
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentCategory"] = category;

            // Връщаме списъка с намерените рецепти
            return View(await query.ToListAsync());
        }

        // ==========================================
        // 2. INSERT (Добавяне на рецепта)
        // ==========================================

        // GET: Показва формата за въвеждане
        public IActionResult Create()
        {
            return View();
        }

        // POST: Записва новата рецепта в базата данни
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Recipe recipe)
        {
            // Проверка дали въведените данни отговарят на правилата в Model-а (Data Annotations)
            if (ModelState.IsValid)
            {
                _context.Recipes.Add(recipe);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index)); // Пренасочва към списъка с рецепти
            }

            // Ако има грешки, връщаме същата форма с въведените данни и съобщения за грешка
            return View(recipe);
        }
    }
}
