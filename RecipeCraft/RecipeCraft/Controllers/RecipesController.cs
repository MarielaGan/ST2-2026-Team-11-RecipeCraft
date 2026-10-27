using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RecipeCraft.Data;
using RecipeCraft.Models;
using RecipeCraft.Services;
using System.Security.Claims;

namespace RecipeCraft.Controllers
{
    public class RecipesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IGeminiQueryService _geminiService;
        private readonly IWebHostEnvironment _webHostEnvironment;

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
            ViewBag.Categories = await _context.Categories.ToListAsync();

            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentCategory"] = category;
            ViewBag.AiQuery = aiQuery;

            if (!string.IsNullOrWhiteSpace(aiQuery))
            {
                string trimmedQuery = aiQuery.Trim();

                var geminiResult = await _geminiService.GenerateJsonQueryAsync(trimmedQuery, CancellationToken.None);

                if (!geminiResult.IsSuccess || geminiResult.Query is null)
                {
                    ViewBag.Error = geminiResult.Error ?? "Възникна грешка при обработката на заявката от ИИ.";
                    return View(await _context.Recipes.Include(r => r.Category).Include(r => r.User).ToListAsync());
                }

                try
                {
                    var sqlCommand = JsonQuerySqlTranslator.Translate(geminiResult.Query);

                    var sqlParams = sqlCommand.Parameters
                        .Select(p => new Microsoft.Data.Sqlite.SqliteParameter(p.Key, p.Value))
                        .ToArray();

                    var aiRecipes = await _context.Recipes
                        .FromSqlRaw(sqlCommand.Sql, sqlParams)
                        .Include(r => r.Category)
                        .Include(r => r.User)
                        .ToListAsync();

                    ViewBag.RawJson = geminiResult.RawJson;
                    ViewBag.GeneratedSql = sqlCommand.ReadableSql;
                    ViewBag.Explanation = geminiResult.Query.Explanation;

                    return View(aiRecipes);
                }
                catch (ArgumentException ex)
                {
                    ViewBag.Error = $"Грешка при превод на JSONQuery към SQL: {ex.Message}";
                    return View(await _context.Recipes.Include(r => r.Category).Include(r => r.User).ToListAsync());
                }
                catch (Exception ex)
                {
                    ViewBag.Error = $"Възникна системна грешка: {ex.Message}";
                    return View(await _context.Recipes.Include(r => r.Category).Include(r => r.User).ToListAsync());
                }
            }

            var query = _context.Recipes
                .Include(r => r.Category)
                .Include(r => r.User)
                .AsQueryable();

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
        // 2. DETAILS (Детайли на рецепта)
        // ==========================================
        [AllowAnonymous]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var recipe = await _context.Recipes
                .Include(r => r.Category)
                .Include(r => r.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (recipe == null) return NotFound();

            return View(recipe);
        }

        // ==========================================
        // 3. INSERT (Добавяне на рецепта)
        // ==========================================
        [Authorize]
        public async Task<IActionResult> Create()
        {
            var categories = await _context.Categories.ToListAsync();
            ViewBag.CategoryId = new SelectList(categories, "Id", "Name");

            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Recipe recipe, IFormFile? imageFile)
        {
            var currentUserId = await GetCurrentUserIdAsync();
            if (currentUserId == null)
            {
                return RedirectToAction("Login", "Users");
            }

            recipe.UserId = currentUserId.Value;
            recipe.CreatedOn = DateTime.Now;

            ModelState.Remove(nameof(Recipe.User));

            if (imageFile != null && imageFile.Length > 0)
            {
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

                recipe.ImageUrl = "/uploads/" + uniqueFileName;
            }

            if (ModelState.IsValid)
            {
                _context.Recipes.Add(recipe);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            var categories = await _context.Categories.ToListAsync();
            ViewBag.CategoryId = new SelectList(categories, "Id", "Name", recipe.CategoryId);

            return View(recipe);
        }

        // ==========================================
        // 4. EDIT (Редакция на рецепта)
        // ==========================================
        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var recipe = await _context.Recipes.FindAsync(id);
            if (recipe == null) return NotFound();

            var currentUserId = await GetCurrentUserIdAsync();
            bool isAdmin = User.IsInRole("Admin") || User.Identity?.Name == "admin";

            if (recipe.UserId != currentUserId && !isAdmin)
            {
                return Forbid();
            }

            var categories = await _context.Categories.ToListAsync();
            ViewBag.CategoryId = new SelectList(categories, "Id", "Name", recipe.CategoryId);

            return View(recipe);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Recipe recipe, IFormFile? imageFile)
        {
            if (id != recipe.Id) return NotFound();

            var existingRecipe = await _context.Recipes.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
            if (existingRecipe == null) return NotFound();

            var currentUserId = await GetCurrentUserIdAsync();
            bool isAdmin = User.IsInRole("Admin") || User.Identity?.Name == "admin";

            if (existingRecipe.UserId != currentUserId && !isAdmin)
            {
                return Forbid();
            }

            recipe.UserId = existingRecipe.UserId;
            recipe.CreatedOn = existingRecipe.CreatedOn;

            ModelState.Remove(nameof(Recipe.User));

            if (recipe.CategoryId == 0)
            {
                recipe.CategoryId = existingRecipe.CategoryId;
            }

            if (imageFile != null && imageFile.Length > 0)
            {
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(imageFile.FileName);
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(fileStream);
                }
                recipe.ImageUrl = "/uploads/" + uniqueFileName;
            }
            else
            {
                recipe.ImageUrl = existingRecipe.ImageUrl;
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
                    if (!await _context.Recipes.AnyAsync(r => r.Id == id)) return NotFound();
                    throw;
                }

                return RedirectToAction(nameof(MyRecipes));
            }

            var categories = await _context.Categories.ToListAsync();
            ViewBag.CategoryId = new SelectList(categories, "Id", "Name", recipe.CategoryId);

            return View(recipe);
        }

        // ==========================================
        // 5. DELETE (Изтриване на рецепта)
        // ==========================================
        [Authorize]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var recipe = await _context.Recipes
                .Include(r => r.Category)
                .Include(r => r.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (recipe == null) return NotFound();

            var currentUserId = await GetCurrentUserIdAsync();
            bool isAdmin = User.IsInRole("Admin") || User.Identity?.Name == "admin";

            if (recipe.UserId != currentUserId && !isAdmin)
            {
                return Forbid();
            }

            return View(recipe);
        }

        [HttpPost, ActionName("Delete")]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var recipe = await _context.Recipes.FindAsync(id);
            if (recipe != null)
            {
                var currentUserId = await GetCurrentUserIdAsync();
                bool isAdmin = User.IsInRole("Admin") || User.Identity?.Name == "admin";

                if (recipe.UserId != currentUserId && !isAdmin)
                {
                    return Forbid();
                }

                _context.Recipes.Remove(recipe);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(MyRecipes));
        }

        // ==========================================
        // 6. MY RECIPES (Рецепти на текущия потребител)
        // ==========================================
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> MyRecipes()
        {
            var currentUserId = await GetCurrentUserIdAsync();
            if (currentUserId == null)
            {
                return RedirectToAction("Login", "Users");
            }

            var myRecipes = await _context.Recipes
                .Include(r => r.Category)
                .Include(r => r.User)
                .Where(r => r.UserId == currentUserId.Value)
                .OrderByDescending(r => r.CreatedOn)
                .ToListAsync();

            return View("Index", myRecipes);
        }

        // ==========================================
        // Помощен метод за извличане на UserId
        // ==========================================
        private async Task<int?> GetCurrentUserIdAsync()
        {
            var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(claimValue, out int userId))
            {
                return userId;
            }

            var username = User.Identity?.Name;
            if (!string.IsNullOrEmpty(username))
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
                return user?.Id;
            }

            return null;
        }
    }
}