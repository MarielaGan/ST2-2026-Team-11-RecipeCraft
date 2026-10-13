using Microsoft.EntityFrameworkCore;
using ST2_2026_Team_10_RecipeCraft.Models;

namespace ST2_2026_Team_10_RecipeCraft.Data
{
    public class ApplicationDbContext:DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<Recipe> Recipes { get; set; }
    }
}
