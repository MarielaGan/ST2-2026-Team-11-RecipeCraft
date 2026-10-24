using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RecipeCraft.Configuration;
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
- ""Instructions"" (начин на приготвяне/инструкции)
- ""PrepTimeMinutes"" (време за приготвяне в минути - числово)

Върни САМО валиден JSON обект със следната структура:
{
  ""logicalOperator"": ""AND"" | ""OR"",
  ""conditions"": [
    { ""field"": ""Title"" | ""Category"" | ""Ingredients"" | ""Instructions"" | ""PrepTimeMinutes"", ""operator"": ""contains"" | ""notContains"" | ""equals"" | ""lessThan"" | ""greaterThan"", ""value"": ""стойност"" }
  ],
  ""orderBy"": ""Title"" | ""Category"" | ""PrepTimeMinutes"" | ""CreatedOn"",
  ""sortDirection"": ""ASC"" | ""DESC"",
  ""limit"": null,
  ""explanation"": ""кратко обяснение на български език""
}
Правила:
1. За позитивно търсене по текст (Title, Category, Ingredients, Instructions) използвай ""contains"".
2. ВАЖНО ЗА ОТРИЦАНИЯ: Когато потребителят изключва нещо (напр. ""без месо"", ""без мляко"", ""не съдържа захар""), задължително използвай operator: ""notContains"".
3. За числово търсене по времетраене (PrepTimeMinutes) използвай ""lessThan"", ""greaterThan"" или ""equals"".
4. Всички стойности във ""value"" ТРЯБВА ДА БЪДАТ В КАВИЧКИ (string), дори когато представляват числа! Пример: ""60"".
5. По подразбиране logicalOperator e ""AND"", orderBy e ""Title"", sortDirection e ""ASC"".";
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
                return GeminiQueryResult.Fail("Липсва Gemini API ключ в User Secrets.");

            var modelName = string.IsNullOrWhiteSpace(_options.GeminiModel) ? "gemini-3.5-flash" : _options.GeminiModel.Trim();
            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";

            var requestBody = new
            {
                systemInstruction = new { parts = new[] { new { text = SystemPrompt } } },
                contents = new[] { new { role = "user", parts = new[] { new { text = naturalLanguageQuery } } } },
                generationConfig = new { temperature = 0.1, responseMimeType = "application/json" }
            };

            try
            {
                var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                HttpResponseMessage response = null!;
                int maxRetries = 3;
                int delayMs = 2000;

                for (int i = 0; i < maxRetries; i++)
                {
                    response = await _http.PostAsync(endpoint, jsonContent, cancellationToken);

                    /*if ((int)response.StatusCode == 429 && i < maxRetries - 1)
                    {
                        await Task.Delay(delayMs, cancellationToken);
                        delayMs *= 2;
                        continue;
                    }*/
                    if (((int)response.StatusCode == 429 || (int)response.StatusCode == 503) && i < maxRetries - 1)
                    {
                        await Task.Delay(delayMs, cancellationToken);
                        delayMs *= 2;
                        continue;
                    }

                    break;
                }

                if (!response.IsSuccessStatusCode)
                    return GeminiQueryResult.Fail($"Грешка от Gemini API (HTTP {(int)response.StatusCode}).");

                var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(responseText);

                var rawText = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                if (string.IsNullOrWhiteSpace(rawText))
                    return GeminiQueryResult.Fail("Празен отговор от ИИ.");

                var cleanedJson = CleanJson(rawText);
                var queryObj = JsonSerializer.Deserialize<JSONQuery>(cleanedJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return queryObj is not null ? GeminiQueryResult.Ok(queryObj, rawText) : GeminiQueryResult.Fail("Невалиден JSONQuery.");
            }
            catch (Exception ex)
            {
                return GeminiQueryResult.Fail($"Възникна грешка при връзката с Gemini: {ex.Message}");
            }
        }
        private static string CleanJson(string text)
        {
            var trimmed = text.Trim();
            if (trimmed.StartsWith("```"))
            {
                var firstLine = trimmed.IndexOf('\n');
                if (firstLine >= 0) trimmed = trimmed[(firstLine + 1)..];
                if (trimmed.EndsWith("```")) trimmed = trimmed[..^3];
            }
            return trimmed.Trim();
        }
    }
}