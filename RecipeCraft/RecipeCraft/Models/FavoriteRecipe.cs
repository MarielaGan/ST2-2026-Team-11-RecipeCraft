using System.ComponentModel.DataAnnotations;

namespace RecipeCraft.Models
{
    public class FavoriteRecipe
    {
        public int Id { get; set; }

        // Идентификатор на потребителя (потребителското име или User ID)
        [Required]
        public string UserName { get; set; } = string.Empty;

        // Външен ключ към Рецептата
        public int RecipeId { get; set; }

        // Навигационно свойство към рецептата
        public Recipe? Recipe { get; set; }

        // Дата на добавяне в любими
        public DateTime AddedOn { get; set; } = DateTime.Now;
    }
}
