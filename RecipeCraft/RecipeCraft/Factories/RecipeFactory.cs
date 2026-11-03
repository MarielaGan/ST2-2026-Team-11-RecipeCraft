using RecipeCraft.Models;

namespace RecipeCraft.Factories
{
    /// <summary>
    /// Интерфейс за Factory Pattern, отговорен за капсулирането на логиката 
    /// по инстанциране и първоначална настройка на Рецепти (Recipe objects).
    /// </summary>
    public interface IRecipeFactory
    {
        /// <summary>
        /// Създава нов обект от тип Recipe с автоматична дата на създаване и снимка по подразбиране.
        /// </summary>
        Recipe CreateRecipe(
            string title,
            int categoryId,
            int userId,
            int prepTimeMinutes,
            string ingredients,
            string instructions,
            string? imageUrl = null);
    }

    /// <summary>
    /// Конкретна реализация на Factory Pattern за Рецепти.
    /// Централизира конструирането на рецепта и задава служебни стойности като дата и подразбиращо се изображение.
    /// </summary>
    public class RecipeFactory : IRecipeFactory
    {
        public Recipe CreateRecipe(
            string title,
            int categoryId,
            int userId,
            int prepTimeMinutes,
            string ingredients,
            string instructions,
            string? imageUrl = null)
        {
            return new Recipe
            {
                Title = title,
                CategoryId = categoryId,
                UserId = userId,
                PrepTimeMinutes = prepTimeMinutes,
                Ingredients = ingredients,
                Instructions = instructions,
                // Задаваме валидна снимка по подразбиране, ако не е качена такава
                ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? "/images/default-recipe.jpg" : imageUrl,
                // Автоматично задаване на текуща дата и час
                CreatedOn = DateTime.Now
            };
        }
    }
}