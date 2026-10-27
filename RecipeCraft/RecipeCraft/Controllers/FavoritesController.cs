using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeCraft.Data;
using RecipeCraft.Models;
using System.Security.Claims;

namespace RecipeCraft.Controllers
{
    [Authorize]
    public class FavoritesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FavoritesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
            {
                return RedirectToAction("Login", "Users");
            }

            var favorites = await _context.FavoriteRecipes
                .Include(f => f.Recipe)
                    .ThenInclude(r => r!.Category)
                .Include(f => f.Recipe)
                    .ThenInclude(r => r!.User)
                .Where(f => f.UserId == userId.Value)
                .ToListAsync();

            return View(favorites);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int recipeId)
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
            {
                return Unauthorized();
            }

            var existingFavorite = await _context.FavoriteRecipes
                .FirstOrDefaultAsync(f => f.RecipeId == recipeId && f.UserId == userId.Value);

            if (existingFavorite != null)
            {
                _context.FavoriteRecipes.Remove(existingFavorite);
            }
            else
            {
                _context.FavoriteRecipes.Add(new FavoriteRecipe
                {
                    RecipeId = recipeId,
                    UserId = userId.Value,
                    AddedOn = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();

            var returnUrl = Request.Headers["Referer"].ToString();
            return !string.IsNullOrEmpty(returnUrl) ? Redirect(returnUrl) : RedirectToAction("Index", "Recipes");
        }

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