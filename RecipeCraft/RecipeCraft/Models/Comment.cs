using System.ComponentModel.DataAnnotations;

namespace RecipeCraft.Models
{
    public class Comment
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Текстът на коментара е задължителен")]
        public string Content { get; set; } = string.Empty;

        public DateTime CreatedOn { get; set; } = DateTime.Now;

        public int UserId { get; set; }
        public User? User { get; set; }

        public int RecipeId { get; set; }
        public Recipe? Recipe { get; set; }
    }
}