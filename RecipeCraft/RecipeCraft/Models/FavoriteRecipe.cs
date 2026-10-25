using System.ComponentModel.DataAnnotations;

namespace RecipeCraft.Models
{
    public class FavoriteRecipe
    {
        public int Id { get; set; }

        // Поправка: използваме UserId (уникален идентификатор)
        [Required]
        public string UserId { get; set; } = string.Empty;

        // Външен ключ към Рецептата
        public int RecipeId { get; set; }
        public Recipe? Recipe { get; set; }

        public DateTime AddedOn { get; set; } = DateTime.Now;
    }
}
