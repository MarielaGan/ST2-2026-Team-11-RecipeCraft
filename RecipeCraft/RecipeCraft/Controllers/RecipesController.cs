using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RecipeCraft.Data;
using RecipeCraft.Models;
using RecipeCraft.Services;

namespace RecipeCraft.Controllers
{
    public class RecipesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IGeminiQueryService _geminiService;
        private readonly IWebHostEnvironment _webHostEnvironment;

     

        // Впръскване на DbContext и Gemini услугата чрез Dependency Injection
        public RecipesController(ApplicationDbContext context, IGeminiQueryService geminiService, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _geminiService = geminiService;
            _webHostEnvironment = webHostEnvironment;
        }

        // ==========================================
        // 1. SELECT WITH FILTER & GEMINI AI SEARCH
        // ==========================================
        [AllowAnonymous]
        public async Task<IActionResult> Index(string? searchString, string? category, string? aiQuery)
        {
            // Зареждаме категориите за падащото меню
            ViewBag.Categories = await _context.Categories.ToListAsync();

            // Запазваме текущите стойности във ViewData / ViewBag за View-то
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentCategory"] = category;
            ViewBag.AiQuery = aiQuery;

            // --- Търсене на естествен език чрез Gemini AI ---
            if (!string.IsNullOrWhiteSpace(aiQuery))
            {
                string trimmedQuery = aiQuery.Trim();

                // Подаваме CancellationToken.None, за да предпазим заявката от преждевременно прекъсване
                var geminiResult = await _geminiService.GenerateJsonQueryAsync(trimmedQuery, CancellationToken.None);

                if (!geminiResult.IsSuccess || geminiResult.Query is null)
                {
                    ViewBag.Error = geminiResult.Error ?? "Възникна грешка при обработката на заявката от ИИ.";
                    return View(await _context.Recipes.Include(r => r.Category).ToListAsync());
                }

                try
                {
                    // Превеждаме JSONQuery до параметризирана SQL команда
                    var sqlCommand = JsonQuerySqlTranslator.Translate(geminiResult.Query);

                    // Преобразуваме параметрите в SqliteParameter за Entity Framework Core
                    var sqlParams = sqlCommand.Parameters
                        .Select(p => new Microsoft.Data.Sqlite.SqliteParameter(p.Key, p.Value))
                        .ToArray();

                    // Изпълняваме параметризирания SQL през EF Core
                    var aiRecipes = await _context.Recipes
                        .FromSqlRaw(sqlCommand.Sql, sqlParams)
                        .Include(r => r.Category)
                        .ToListAsync();

                    // Подаваме диагностичните данни към View-то (за демонстрацията пред преподавателя)
                    ViewBag.RawJson = geminiResult.RawJson;
                    ViewBag.GeneratedSql = sqlCommand.ReadableSql;
                    ViewBag.Explanation = geminiResult.Query.Explanation;

                    return View(aiRecipes);
                }
                catch (ArgumentException ex)
                {
                    ViewBag.Error = $"Грешка при превод на JSONQuery към SQL: {ex.Message}";
                    return View(await _context.Recipes.Include(r => r.Category).ToListAsync());
                }
                catch (Exception ex)
                {
                    ViewBag.Error = $"Възникна системна грешка: {ex.Message}";
                    return View(await _context.Recipes.Include(r => r.Category).ToListAsync());
                }
            }

            // --- Стандартно филтриране (без ИИ) ---
            var query = _context.Recipes.Include(r => r.Category).AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                query = query.Where(r => r.Title.Contains(searchString) || r.Ingredients.Contains(searchString));
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(r => r.Category != null && r.Category.Name == category);
            }

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
        public async Task<IActionResult> Create(Recipe recipe, IFormFile? imageFile)
        {

            recipe.CreatedBy = User.Identity?.Name ?? "Анонимен";
            recipe.CreatedOn = DateTime.Now;


            // Обработка на качената снимка
            if (imageFile != null && imageFile.Length > 0)
            {
                // Генерираме уникално име за файла, за да няма препокриване
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(imageFile.FileName);
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(fileStream);
                }

                // Записваме относителния път за визуализация в HTML
                recipe.ImageUrl = "/uploads/" + uniqueFileName;
            }
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