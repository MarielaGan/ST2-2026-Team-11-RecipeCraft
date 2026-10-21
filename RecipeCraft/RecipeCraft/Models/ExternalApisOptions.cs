namespace RecipeCraft.Models
{
    public class ExternalApisOptions
    {
        public const string SectionName = "ExternalApis";
        public string? GeminiApiKey { get; set; }
        public string GeminiModel { get; set; } = "gemini-3-flash-preview";
    }
}
