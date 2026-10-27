using RecipeCraft.Models;

namespace RecipeCraft.Data
{
    /// <summary>
    /// DESIGN PATTERN: REPOSITORY / DATABASE INTERFACE
    /// Описание: Централизиран интерфейс за достъп до данните според архитектурата от лекцията.
    /// Клиентите (контролерите) познават само този интерфейс.
    /// </summary>
    public interface IDatabase
    {
        // --- РЕЦЕПТИ ---
        Task<IEnumerable<Recipe>> GetAllRecipesWithDetailsAsync();
        Task<IEnumerable<Recipe>> GetFilteredRecipesAsync(string? searchString, string? category);
        Task<IEnumerable<Recipe>> GetRecipesFromSqlRawAsync(string sql, object[] parameters);
        Task<IEnumerable<Recipe>> GetRecipesByUserIdAsync(int userId);
        Task<Recipe?> GetRecipeByIdAsync(int id);
        Task<Recipe?> GetRecipeByIdWithDetailsAsync(int id);
        Task<Recipe?> GetRecipeByIdAsNoTrackingAsync(int id);
        Task AddRecipeAsync(Recipe recipe);
        Task UpdateRecipeAsync(Recipe recipe);
        Task DeleteRecipeAsync(int id);
        Task<bool> RecipeExistsAsync(int id);

        // --- КАТЕГОРИИ ---
        Task<IEnumerable<Category>> GetAllCategoriesAsync();
        Task<Category?> GetCategoryByIdAsync(int id);
        Task AddCategoryAsync(Category category);
        Task UpdateCategoryAsync(Category category);
        Task DeleteCategoryAsync(int id);

        // --- ЛЮБИМИ РЕЦЕПТИ ---
        Task<IEnumerable<FavoriteRecipe>> GetFavoritesByUserIdAsync(int userId);
        Task ToggleFavoriteAsync(int recipeId, int userId);

        // --- ПОТРЕБИТЕЛИ ---
        Task<IEnumerable<User>> GetFilteredUsersAsync(string? searchString, string? roleFilter);
        Task<User?> GetUserByIdAsync(int id);
        Task<User?> GetUserByUsernameAsync(string username);
        Task<bool> UserExistsByUsernameAsync(string username, int? excludeUserId = null);
        Task<bool> UserExistsByEmailAsync(string email, int? excludeUserId = null);
        Task<bool> UserExistsByIdAsync(int id);
        Task AddUserAsync(User user);
        Task UpdateUserAsync(User user);
        Task DeleteUserAsync(int id);
    }
}