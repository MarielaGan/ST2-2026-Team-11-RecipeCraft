using System.ComponentModel.DataAnnotations;

namespace RecipeCraft.Models
{
    public class FavoriteRecipe
    {
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }
        public User? User { get; set; }

        // Външен ключ към Рецептата
        public int RecipeId { get; set; }
        public Recipe? Recipe { get; set; }

        public DateTime AddedOn { get; set; } = DateTime.Now;
    }
}
