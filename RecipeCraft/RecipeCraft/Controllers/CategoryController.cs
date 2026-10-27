using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecipeCraft.Data;
using RecipeCraft.Models;

namespace RecipeCraft.Controllers
{
    [Authorize(Roles = "Admin")]
    public class CategoryController : Controller
    {
        private readonly IDatabase _database;

        public CategoryController(IDatabase database)
        {
            _database = database;
        }

        // 1. Списък на всички категории (Index)
        public async Task<IActionResult> Index()
        {
            var categories = await _database.GetAllCategoriesAsync();
            return View(categories);
        }

        // 2. Форма за създаване на нова категория (GET)
        public IActionResult Create()
        {
            return View();
        }

        // 3. Запазване на новата категория (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category category)
        {
            if (ModelState.IsValid)
            {
                await _database.AddCategoryAsync(category);
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }

        // 4. Форма за редактиране (GET)
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var category = await _database.GetCategoryByIdAsync(id.Value);
            if (category == null) return NotFound();

            return View(category);
        }

        // 5. Запазване на редакцията (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Category category)
        {
            if (id != category.Id) return NotFound();

            if (ModelState.IsValid)
            {
                await _database.UpdateCategoryAsync(category);
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }

        // 6. Изтриване на категория (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            await _database.DeleteCategoryAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }
}