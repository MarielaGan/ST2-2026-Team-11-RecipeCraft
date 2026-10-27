using Microsoft.EntityFrameworkCore;
using RecipeCraft.Data;
using RecipeCraft.Models;

namespace RecipeCraft.Data
{


    /// <summary>
    /// Конкретна SQLite / Entity Framework Core имплементация на IDatabase.
    /// </summary>
    public class Database : IDatabase
    {
        private readonly ApplicationDbContext _context;

        public Database(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // 1. РЕЦЕПТИ
        // ==========================================
        public async Task<IEnumerable<Recipe>> GetAllRecipesWithDetailsAsync()
        {
            return await _context.Recipes
                .Include(r => r.Category)
                .Include(r => r.User)
                .ToListAsync();
        }

        public async Task<IEnumerable<Recipe>> GetFilteredRecipesAsync(string? searchString, string? category)
        {
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

            return await query.ToListAsync();
        }

        public async Task<IEnumerable<Recipe>> GetRecipesFromSqlRawAsync(string sql, object[] parameters)
        {
            return await _context.Recipes
                .FromSqlRaw(sql, parameters)
                .Include(r => r.Category)
                .Include(r => r.User)
                .ToListAsync();
        }

        public async Task<IEnumerable<Recipe>> GetRecipesByUserIdAsync(int userId)
        {
            return await _context.Recipes
                .Include(r => r.Category)
                .Include(r => r.User)
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedOn)
                .ToListAsync();
        }

        public async Task<Recipe?> GetRecipeByIdAsync(int id)
        {
            return await _context.Recipes.FindAsync(id);
        }

        public async Task<Recipe?> GetRecipeByIdWithDetailsAsync(int id)
        {
            return await _context.Recipes
                .Include(r => r.Category)
                .Include(r => r.User)
                .FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<Recipe?> GetRecipeByIdAsNoTrackingAsync(int id)
        {
            return await _context.Recipes
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task AddRecipeAsync(Recipe recipe)
        {
            await _context.Recipes.AddAsync(recipe);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateRecipeAsync(Recipe recipe)
        {
            _context.Update(recipe);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteRecipeAsync(int id)
        {
            var recipe = await _context.Recipes.FindAsync(id);
            if (recipe != null)
            {
                _context.Recipes.Remove(recipe);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> RecipeExistsAsync(int id)
        {
            return await _context.Recipes.AnyAsync(r => r.Id == id);
        }

        // ==========================================
        // 2. КАТЕГОРИИ
        // ==========================================
        public async Task<IEnumerable<Category>> GetAllCategoriesAsync()
        {
            return await _context.Categories.ToListAsync();
        }

        public async Task<Category?> GetCategoryByIdAsync(int id)
        {
            return await _context.Categories.FindAsync(id);
        }

        public async Task AddCategoryAsync(Category category)
        {
            await _context.Categories.AddAsync(category);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateCategoryAsync(Category category)
        {
            _context.Categories.Update(category);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteCategoryAsync(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category != null)
            {
                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
            }
        }
        // ==========================================
        // 3. ЛЮБИМИ РЕЦЕПТИ
        // ==========================================
        public async Task<IEnumerable<FavoriteRecipe>> GetFavoritesByUserIdAsync(int userId)
        {
            return await _context.FavoriteRecipes
                .Include(f => f.Recipe)
                    .ThenInclude(r => r!.Category)
                .Include(f => f.Recipe)
                    .ThenInclude(r => r!.User)
                .Where(f => f.UserId == userId)
                .ToListAsync();
        }

        public async Task ToggleFavoriteAsync(int recipeId, int userId)
        {
            var existingFavorite = await _context.FavoriteRecipes
                .FirstOrDefaultAsync(f => f.RecipeId == recipeId && f.UserId == userId);

            if (existingFavorite != null)
            {
                _context.FavoriteRecipes.Remove(existingFavorite);
            }
            else
            {
                _context.FavoriteRecipes.Add(new FavoriteRecipe
                {
                    RecipeId = recipeId,
                    UserId = userId,
                    AddedOn = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();
        }

        // ==========================================
        // 4. ПОТРЕБИТЕЛИ
        // ==========================================
        public async Task<IEnumerable<User>> GetFilteredUsersAsync(string? searchString, string? roleFilter)
        {
            var query = _context.Users.AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                searchString = searchString.ToLower();
                query = query.Where(u => u.Username.ToLower().Contains(searchString)
                                      || u.FirstName.ToLower().Contains(searchString)
                                      || u.LastName.ToLower().Contains(searchString)
                                      || u.Email.ToLower().Contains(searchString));
            }

            if (!string.IsNullOrEmpty(roleFilter))
            {
                query = query.Where(u => u.Role == roleFilter);
            }

            return await query.ToListAsync();
        }

        public async Task<User?> GetUserByIdAsync(int id)
        {
            return await _context.Users.FindAsync(id);
        }

        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        }

        public async Task<bool> UserExistsByUsernameAsync(string username, int? excludeUserId = null)
        {
            if (excludeUserId.HasValue)
            {
                return await _context.Users.AnyAsync(u => u.Username == username && u.Id != excludeUserId.Value);
            }
            return await _context.Users.AnyAsync(u => u.Username == username);
        }

        public async Task<bool> UserExistsByEmailAsync(string email, int? excludeUserId = null)
        {
            if (excludeUserId.HasValue)
            {
                return await _context.Users.AnyAsync(u => u.Email == email && u.Id != excludeUserId.Value);
            }
            return await _context.Users.AnyAsync(u => u.Email == email);
        }

        public async Task<bool> UserExistsByIdAsync(int id)
        {
            return await _context.Users.AnyAsync(u => u.Id == id);
        }

        public async Task AddUserAsync(User user)
        {
            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateUserAsync(User user)
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteUserAsync(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
            }
        }
    }
}
