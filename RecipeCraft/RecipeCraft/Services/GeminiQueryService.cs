using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RecipeCraft.Models;

namespace RecipeCraft.Services
{
    public sealed class GeminiQueryService : IGeminiQueryService
    {
        private readonly HttpClient _http;
        private readonly ExternalApisOptions _options;

        private const string SystemPrompt = @"
Ти си интелигентен асистент за преобразуване на заявки на естествен език в структуриран JSONQuery обект за търсене на рецепти в SQLite база данни.

Разрешени полета (Field):
- ""Title"" (заглавие/име на рецептата)
- ""Category"" (категория)
- ""Ingredients"" (съставки)

Върни САМО валиден JSON обект със следната структура:
{
  ""logicalOperator"": ""AND"" | ""OR"",
  ""conditions"": [
    { ""field"": ""Title"" | ""Category"" | ""Ingredients"", ""operator"": ""contains"" | ""equals"", ""value"": ""стойност"" }
  ],
  ""orderBy"": ""Title"" | ""Category"",
  ""sortDirection"": ""ASC"" | ""DESC"",
  ""limit"": null,
  ""explanation"": ""кратко обяснение на български език""
}
Правила:
1. За търсене по съставка или име използвай ""contains"".
2. По подразбиране logicalOperator e ""AND"", orderBy e ""Title"", sortDirection e ""ASC"".";

        public GeminiQueryService(HttpClient http, IOptions<ExternalApisOptions> options)
        {
            _http = http;
            _options = options.Value;
        }

        public async Task<GeminiQueryResult> GenerateJsonQueryAsync(string naturalLanguageQuery, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(naturalLanguageQuery))
                return GeminiQueryResult.Fail("Моля, въведете заявка на естествен език.");

            var apiKey = _options.GeminiApiKey?.Trim();
            if (string.IsNullOrWhiteSpace(apiKey))
                return GeminiQueryResult.Fail("Липсва Gemini API ключ в appsettings.Development.json.");

            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{_options.GeminiModel}:generateContent?key={apiKey}";

            var requestBody = new
            {
                systemInstruction = new { parts = new[] { new { text = SystemPrompt } } },
                contents = new[] { new { role = "user", parts = new[] { new { text = naturalLanguageQuery } } } },
                generationConfig = new { temperature = 0.1, responseMimeType = "application/json" }
            };

            try
            {
                var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                // Използваме CancellationToken.None или контролен токен, за да избегнем прекъсване
                var response = await _http.PostAsync(endpoint, jsonContent, CancellationToken.None);

                if (!response.IsSuccessStatusCode)
                    return GeminiQueryResult.Fail($"Грешка от Gemini API (HTTP {(int)response.StatusCode}).");

                var responseText = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseText);

                var rawText = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
                if (string.IsNullOrWhiteSpace(rawText))
                    return GeminiQueryResult.Fail("Празен отговор от ИИ.");

                var queryObj = JsonSerializer.Deserialize<JSONQuery>(rawText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return queryObj is not null ? GeminiQueryResult.Ok(queryObj, rawText) : GeminiQueryResult.Fail("Невалиден JSONQuery.");
            }
            catch (Exception ex)
            {
                return GeminiQueryResult.Fail($"Възникна грешка при връзката с Gemini: {ex.Message}");
            }
        }
    }
}