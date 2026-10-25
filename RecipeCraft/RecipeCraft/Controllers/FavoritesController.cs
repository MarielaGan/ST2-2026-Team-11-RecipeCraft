using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeCraft.Data;
using RecipeCraft.Models;
using System.Security.Claims; // Нужен за ClaimTypes.NameIdentifier

namespace RecipeCraft.Controllers
{
    [Authorize] // Само за влезели потребители
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
            // Вземаме уникалното UserId
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var favorites = await _context.FavoriteRecipes
                .Include(f => f.Recipe)
                    .ThenInclude(r => r!.Category)
                .Where(f => f.UserId == userId) // Филтриране по UserId
                .ToListAsync();

            return View(favorites);
        }

        // 2. Добавяне / Премахване от любими (Toggle)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int recipeId)
        {
            // Вземаме уникалното UserId
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var existingFavorite = await _context.FavoriteRecipes
                .FirstOrDefaultAsync(f => f.RecipeId == recipeId && f.UserId == userId);

            if (existingFavorite != null)
            {
                // Премахваме от любими, ако вече я има
                _context.FavoriteRecipes.Remove(existingFavorite);
            }
            else
            {
                // Добавяме в любими с уникалното UserId
                _context.FavoriteRecipes.Add(new FavoriteRecipe
                {
                    RecipeId = recipeId,
                    UserId = userId
                });
            }

            await _context.SaveChangesAsync();

            // Връщаме потребителя към страницата, от която е извикал действието
            var returnUrl = Request.Headers["Referer"].ToString();
            return !string.IsNullOrEmpty(returnUrl) ? Redirect(returnUrl) : RedirectToAction("Index", "Recipes");
        }
    }
}