using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeCraft.Data;
using RecipeCraft.Models;

namespace RecipeCraft.Controllers
{
    [Authorize] // Само влезели потребители имат любими рецепти
    public class FavoritesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FavoritesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. Списък с всички любими рецепти на текущия потребител
        public async Task<IActionResult> Index()
        {
            var userName = User.Identity?.Name;

            var favorites = await _context.FavoriteRecipes
                .Include(f => f.Recipe)
                    .ThenInclude(r => r!.Category)
                .Where(f => f.UserName == userName)
                .ToListAsync();

            return View(favorites);
        }

        // 2. Добавяне/Премахване от любими (Toggle)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int recipeId)
        {
            var userName = User.Identity?.Name;
            if (string.IsNullOrEmpty(userName)) return Unauthorized();

            var existingFavorite = await _context.FavoriteRecipes
                .FirstOrDefaultAsync(f => f.RecipeId == recipeId && f.UserName == userName);

            if (existingFavorite != null)
            {
                // Премахваме от любими, ако вече я има
                _context.FavoriteRecipes.Remove(existingFavorite);
            }
            else
            {
                // Добавяме в любими
                _context.FavoriteRecipes.Add(new FavoriteRecipe
                {
                    RecipeId = recipeId,
                    UserName = userName
                });
            }

            await _context.SaveChangesAsync();

            // Връщаме потребителя към страницата, от която е извикал действието
            var returnUrl = Request.Headers["Referer"].ToString();
            return !string.IsNullOrEmpty(returnUrl) ? Redirect(returnUrl) : RedirectToAction("Index", "Recipes");
        }
    }
}