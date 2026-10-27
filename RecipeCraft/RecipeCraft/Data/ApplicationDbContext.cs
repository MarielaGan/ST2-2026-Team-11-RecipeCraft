using Microsoft.EntityFrameworkCore;
using RecipeCraft.Models;

namespace RecipeCraft.Data
{
    public class ApplicationDbContext:DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<Recipe> Recipes { get; set; }
        public DbSet<User> Users { get; set; }       
        public DbSet<Category> Categories { get; set; }
        public DbSet<FavoriteRecipe> FavoriteRecipes { get; set; }
        public DbSet<Comment> Comments { get; set; }
    }
}
