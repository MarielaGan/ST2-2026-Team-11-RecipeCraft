using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecipeCraft.Data;
using RecipeCraft.Models;
using RecipeCraft.Services;
using System.Security.Claims;

namespace RecipeCraft.Controllers
{
    [Authorize]
    public class CommentsController : Controller
    {
        private readonly IDatabase _db;
        private readonly IEmailSender _emailSender;

        public CommentsController(IDatabase db, IEmailSender emailSender)
        {
            _db = db;
            _emailSender = emailSender;
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
                var user = await _db.GetUserByUsernameAsync(User.Identity.Name);
                if (user != null) userId = user.Id;
            }

            if (userId == 0)
            {
                return RedirectToAction("Login", "Users");
            }

            // 1. Запазване на коментара в базата
            var comment = new Comment
            {
                RecipeId = recipeId,
                UserId = userId,
                Content = content.Trim(),
                CreatedOn = DateTime.Now
            };

            await _db.AddCommentAsync(comment);

            // 2. Изпращане на имейл известие към автора на рецептата
            try
            {
                var recipe = await _db.GetRecipeByIdAsync(recipeId);

                // Изпраща се само ако коментиращият НЕ Е самият автор на рецептата
                if (recipe != null && recipe.UserId != userId)
                {
                    var recipeOwner = recipe.User ?? await _db.GetUserByIdAsync(recipe.UserId);
                    var currentUsername = User.Identity?.Name ?? "Потребител";

                    if (recipeOwner != null && !string.IsNullOrWhiteSpace(recipeOwner.Email))
                    {
                        string subject = $"Нов коментар за вашата рецепта \"{recipe.Title}\"";
                        string emailBody = $@"
                            <div style='font-family: Arial, sans-serif; color: #333;'>
                                <h2>Здравейте, {recipeOwner.Username}!</h2>
                                <p>Потребителят <strong>{currentUsername}</strong> остави нов коментар под вашата рецепта <strong><{recipe.Title}></strong>:</p>
                                <blockquote style='background-color: #fff8e1; padding: 12px; border-left: 4px solid #ffc107; font-style: italic; margin: 15px 0;'>
                                    ""{content.Trim()}""
                                </blockquote>
                                <p>Влезте в RecipeCraft, за да прегледате пълната рецепта и отговорите!</p>
                            </div>";

                        await _emailSender.SendEmailAsync(recipeOwner.Email, subject, emailBody);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EMAIL ERROR] Грешка при известяване: {ex.Message}");
            }

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