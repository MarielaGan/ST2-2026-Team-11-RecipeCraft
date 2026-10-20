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
        // Добавете тази линия:
        public DbSet<Category> Categories { get; set; }
    }
}
