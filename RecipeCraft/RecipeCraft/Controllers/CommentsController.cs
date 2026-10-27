using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecipeCraft.Data;
using RecipeCraft.Models;
using System.Security.Claims;

namespace RecipeCraft.Controllers
{
    [Authorize]
    public class CommentsController : Controller
    {
        private readonly IDatabase _db;

        public CommentsController(IDatabase db)
        {
            _db = db;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int recipeId, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return RedirectToAction("Details", "Recipes", new { id = recipeId });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int userId = 0;

            if (int.TryParse(userIdClaim, out int parsedId))
            {
                userId = parsedId;
            }
            else if (!string.IsNullOrEmpty(User.Identity?.Name))
            {
                var user = await _db.GetUserByUsernameAsync(User.Identity.Name); // или през context
                if (user != null) userId = user.Id;
            }

            if (userId == 0)
            {
                return RedirectToAction("Login", "Users");
            }

            var comment = new Comment
            {
                RecipeId = recipeId,
                UserId = userId,
                Content = content.Trim(),
                CreatedOn = DateTime.Now
            };

            await _db.AddCommentAsync(comment);

            return RedirectToAction("Details", "Recipes", new { id = recipeId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, int recipeId)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int.TryParse(userIdClaim, out int userId);
            bool isAdmin = User.IsInRole("Admin") || User.Identity?.Name == "admin";

            await _db.DeleteCommentAsync(id, userId, isAdmin);

            return RedirectToAction("Details", "Recipes", new { id = recipeId });
        }
    }
}