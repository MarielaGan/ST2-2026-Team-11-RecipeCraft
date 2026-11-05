using RecipeCraft.Models;

namespace RecipeCraft.Services
{
    public sealed record GeminiQueryResult(JSONQuery? Query, string? RawJson, string? Error)
    {
        public bool IsSuccess => Query is not null && Error is null;
        public static GeminiQueryResult Ok(JSONQuery query, string rawJson) => new(query, rawJson, null);
        public static GeminiQueryResult Fail(string error) => new(null, null, error);
    }

    public interface IGeminiQueryService
    {
        Task<GeminiQueryResult> GenerateJsonQueryAsync(string naturalLanguageQuery, CancellationToken cancellationToken = default);

        Task<(bool IsMatch, string Reason)> ValidateRecipeImageAsync(IFormFile imageFile, string recipeTitle, string ingredients, CancellationToken cancellationToken);
    }
}