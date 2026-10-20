using System.ComponentModel.DataAnnotations;

namespace RecipeCraft.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Името на категорията е задължително.")]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        // Навигационно свойство: Връзка към всички рецепти в тази категория
        public ICollection<Recipe> Recipes { get; set; } = new List<Recipe>();
    }
}

