using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecipeCraft.Data;
using System.Security.Claims;

namespace RecipeCraft.Controllers
{
    [Authorize]
    public class FavoritesController : Controller
    {
        private readonly IDatabase _database;

        public FavoritesController(IDatabase database)
        {
            _database = database;
        }

        public async Task<IActionResult> Index()
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
            {
                return RedirectToAction("Login", "Users");
            }

            // Използваме IDatabase вместо _context.FavoriteRecipes
            var favorites = await _database.GetFavoritesByUserIdAsync(userId.Value);

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

            // Логиката за добавяне/премахване е изнесена изцяло в IDatabase
            await _database.ToggleFavoriteAsync(recipeId, userId.Value);

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
                // Използваме IDatabase за намиране на потребителя
                var user = await _database.GetUserByUsernameAsync(username);
                return user?.Id;
            }

            return null;
        }
    }
}