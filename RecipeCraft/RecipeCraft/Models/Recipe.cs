using System.ComponentModel.DataAnnotations;

namespace RecipeCraft.Models
{
    public class Recipe
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Името на рецептата е задължително")]
        [Display(Name = "Име на рецептата")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Категорията е задължителна")]
        [Display(Name = "Категория")]
       
        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        [Required(ErrorMessage = "Времето за приготвяне е задължително")]
        [Display(Name = "Време за приготвяне (мин)")]
        [Range(1, 1440, ErrorMessage = "Времето трябва да е поне 1 минута")]
        public int PrepTimeMinutes { get; set; }

        [Required(ErrorMessage = "Съставките са задължителни")]
        [Display(Name = "Необходими продукти")]
        public string Ingredients { get; set; } = string.Empty;

        [Required(ErrorMessage = "Инструкциите са задължителни")]
        [Display(Name = "Начин на приготвяне")]
        public string Instructions { get; set; } = string.Empty;
    }
}
