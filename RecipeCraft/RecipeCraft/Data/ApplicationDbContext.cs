using Microsoft.EntityFrameworkCore;
using RecipeCraft.Models;

namespace RecipeCraft.Data
{
    public class ApplicationDbContext:DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<Recipe> Recipes { get; set; }
    }
}
