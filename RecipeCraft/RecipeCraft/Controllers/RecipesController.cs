using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RecipeCraft.Data;
using RecipeCraft.Factories;
using RecipeCraft.Models;
using RecipeCraft.Services;
using System.Security.Claims;

namespace RecipeCraft.Controllers
{
    public class RecipesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IDatabase _database;
        private readonly IRecipeFactory _recipeFactory;
        private readonly IGeminiQueryService _geminiService;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public RecipesController(
            ApplicationDbContext context,
            IDatabase database,
            IRecipeFactory recipeFactory,
            IGeminiQueryService geminiService,
            IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _database = database;
            _recipeFactory = recipeFactory;
            _geminiService = geminiService;
            _webHostEnvironment = webHostEnvironment;
        }

        // ==========================================
        // 1. SELECT WITH FILTER, GEMINI AI SEARCH & PAGINATION
        // ==========================================
        [AllowAnonymous]
        public async Task<IActionResult> Index(string? searchString, string? category, string? aiQuery, int? pageNumber)
        {
            ViewBag.Categories = await _context.Categories.ToListAsync();

            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentCategory"] = category;
            ViewBag.AiQuery = aiQuery;

            int pageSize = AppSettingsManager.Instance.Settings.RecipesPerPage;
            if (pageSize <= 0) pageSize = 3;

            int pageIndex = pageNumber ?? 1;

            if (!string.IsNullOrWhiteSpace(aiQuery))
            {
                string trimmedQuery = aiQuery.Trim();

                var geminiResult = await _geminiService.GenerateJsonQueryAsync(trimmedQuery, CancellationToken.None);

                if (!geminiResult.IsSuccess || geminiResult.Query is null)
                {
                    ViewBag.Error = geminiResult.Error ?? "Възникна грешка при обработката на заявката от ИИ.";
                    var fallbackQuery = _context.Recipes.Include(r => r.Category).Include(r => r.User).AsQueryable();
                    return View(await PaginatedList<Recipe>.CreateAsync(fallbackQuery.AsNoTracking(), pageIndex, pageSize));
                }

                try
                {
                    var sqlCommand = JsonQuerySqlTranslator.Translate(geminiResult.Query);

                    var sqlParams = sqlCommand.Parameters
                        .Select(p => new Microsoft.Data.Sqlite.SqliteParameter(p.Key, p.Value))
                        .ToArray();

                    var aiRecipesQuery = _context.Recipes
                        .FromSqlRaw(sqlCommand.Sql, sqlParams)
                        .Include(r => r.Category)
                        .Include(r => r.User)
                        .AsQueryable();

                    ViewBag.RawJson = geminiResult.RawJson;
                    ViewBag.GeneratedSql = sqlCommand.ReadableSql;
                    ViewBag.Explanation = geminiResult.Query.Explanation;

                    return View(await PaginatedList<Recipe>.CreateAsync(aiRecipesQuery.AsNoTracking(), pageIndex, pageSize));
                }
                catch (ArgumentException ex)
                {
                    ViewBag.Error = $"Грешка при превод на JSONQuery към SQL: {ex.Message}";
                    var fallbackQuery = _context.Recipes.Include(r => r.Category).Include(r => r.User).AsQueryable();
                    return View(await PaginatedList<Recipe>.CreateAsync(fallbackQuery.AsNoTracking(), pageIndex, pageSize));
                }
                catch (Exception ex)
                {
                    ViewBag.Error = $"Възникна системна грешка: {ex.Message}";
                    var fallbackQuery = _context.Recipes.Include(r => r.Category).Include(r => r.User).AsQueryable();
                    return View(await PaginatedList<Recipe>.CreateAsync(fallbackQuery.AsNoTracking(), pageIndex, pageSize));
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

            return View(await PaginatedList<Recipe>.CreateAsync(query.AsNoTracking(), pageIndex, pageSize));
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
                .Include(r => r.Comments)
                    .ThenInclude(c => c.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (recipe == null) return NotFound();

            return View(recipe);
        }

        // ==========================================
        // 3. INSERT (Добавяне на рецепта с ИИ проверка)
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

            ModelState.Remove(nameof(Recipe.User));

            // 💡 1. МУЛТИМОДАЛНА ПРОВЕРКА С ИИ ПРЕДИ ВСЯКАКВИ ДРУГИ ДЕЙСТВИЯ
            if (imageFile != null && imageFile.Length > 0)
            {
                var (isMatch, reason) = await _geminiService.ValidateRecipeImageAsync(
                    imageFile,
                    recipe.Title,
                    recipe.Ingredients,
                    HttpContext.RequestAborted);

                if (!isMatch)
                {
                    // Добавяме грешката към фаталното състояние на модела
                    ModelState.AddModelError("ImageUrl", $"ИИ проверка: {reason}");
                    ModelState.AddModelError(string.Empty, $"Снимката не отговаря на рецептата: {reason}");

                    ViewBag.AiValidationError = reason;

                    var categoriesList = await _context.Categories.ToListAsync();
                    ViewBag.CategoryId = new SelectList(categoriesList, "Id", "Name", recipe.CategoryId);

                    // Спираме процеса тук - снимката не се запазва на диска и не се прави запис
                    return View(recipe);
                }
            }

            // 💡 2. ЗАПИСВАНЕ НА СНИМКАТА НА ДИСКА САМО ПРИ УСПЕШНА ПРОВЕРКА
            string? uploadedImageUrl = null;
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

                uploadedImageUrl = "/uploads/" + uniqueFileName;
            }

            // 💡 3. ВАЛИДАЦИЯ И СЪЗДАВАНЕ ЧРЕЗ FACTORY PATTERN
            if (ModelState.IsValid)
            {
                var newRecipe = _recipeFactory.CreateRecipe(
                    recipe.Title,
                    recipe.CategoryId,
                    currentUserId.Value,
                    recipe.PrepTimeMinutes,
                    recipe.Ingredients,
                    recipe.Instructions,
                    uploadedImageUrl
                );

                _context.Recipes.Add(newRecipe);
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
        public async Task<IActionResult> MyRecipes(int? pageNumber)
        {
            var currentUserId = await GetCurrentUserIdAsync();
            if (currentUserId == null)
            {
                return RedirectToAction("Login", "Users");
            }

            int pageSize = AppSettingsManager.Instance.Settings.RecipesPerPage;
            if (pageSize <= 0) pageSize = 3;

            int pageIndex = pageNumber ?? 1;

            var myRecipesQuery = _context.Recipes
                .Include(r => r.Category)
                .Include(r => r.User)
                .Where(r => r.UserId == currentUserId.Value)
                .OrderByDescending(r => r.CreatedOn)
                .AsQueryable();

            return View("Index", await PaginatedList<Recipe>.CreateAsync(myRecipesQuery.AsNoTracking(), pageIndex, pageSize));
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
                var user = await _database.GetUserByUsernameAsync(username);
                return user?.Id;
            }

            return null;
        }
    }
}