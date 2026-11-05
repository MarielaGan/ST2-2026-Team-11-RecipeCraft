using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using RecipeCraft.Configuration;
using RecipeCraft.Models;

namespace RecipeCraft.Services
{
    public sealed class GeminiQueryService : IGeminiQueryService
    {
        private readonly HttpClient _http;
        private readonly ExternalApisOptions _options;

        // Модел по подразбиране (стабилен) и резервни модели при претоварване
        private const string DefaultModel = "gemini-3.6-flash";
        private static readonly string[] FallbackModels = { "gemini-3.5-flash-lite", "gemini-3.1-flash-lite" };

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
5. По подразбиране logicalOperator e ""AND"", orderBy e ""Title"", sortDirection e ""ASC"".
6. ВАЖНО ЗА КАТЕГОРИИ: Когато потребителят търси по категория (напр. """"основно"""", """"десерт"""", """"супа""""), стойността във """"value"""" ТРЯБВА ЗАДЪЛЖИТЕЛНО да започва с ГЛАВНА буква (напр. """"Основно"""", """"Десерти"""", """"Супи"""", """"Предястия"""")."";";


        public GeminiQueryService(HttpClient http, IOptions<ExternalApisOptions> options)
        {
            _http = http;
            _options = options.Value;
        }

        // =========================================================================
        // Превръщане на заявка на естествен език в JSONQuery
        // =========================================================================
        public async Task<GeminiQueryResult> GenerateJsonQueryAsync(string naturalLanguageQuery, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(naturalLanguageQuery))
                return GeminiQueryResult.Fail("Моля, въведете заявка на естествен език.");

            var apiKey = _options.GeminiApiKey?.Trim();
            if (string.IsNullOrWhiteSpace(apiKey))
                return GeminiQueryResult.Fail("Липсва Gemini API ключ в настройките.");

            var modelName = GetConfiguredModel();
            var endpoint = BuildEndpoint(modelName, apiKey);

            var requestBody = new
            {
                systemInstruction = new { parts = new[] { new { text = SystemPrompt } } },
                contents = new[] { new { role = "user", parts = new[] { new { text = naturalLanguageQuery } } } },
                generationConfig = new { temperature = 0.1, responseMimeType = "application/json" }

            };

            try
            {
                var requestJson = JsonSerializer.Serialize(requestBody);
                HttpResponseMessage response = null!;
                int maxRetries = 3;
                int delayMs = 2000;

                for (int i = 0; i < maxRetries; i++)
                {
                    using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
                    response = await _http.PostAsync(endpoint, content, cancellationToken);

                    if (IsOverloaded(response) && i < maxRetries - 1)
                    {
                        response.Dispose();
                        await Task.Delay(delayMs, cancellationToken);
                        delayMs *= 2;
                        continue;
                    }

                    break;
                }

                if (!response.IsSuccessStatusCode)
                    return GeminiQueryResult.Fail($"Грешка от Gemini API (HTTP {(int)response.StatusCode}).");

                var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
                var rawText = ExtractText(responseText);

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

        // =========================================================================
        // Мултимодална функционалност: валидиране на снимка спрямо рецепта
        // =========================================================================
        public async Task<(bool IsMatch, string Reason)> ValidateRecipeImageAsync(
    IFormFile imageFile,
    string recipeTitle,
    string ingredients,
    CancellationToken cancellationToken = default)
        {
            if (imageFile == null || imageFile.Length == 0)
                return (true, "Няма качена снимка за валидация.");

            var apiKey = _options.GeminiApiKey?.Trim();
            if (string.IsNullOrWhiteSpace(apiKey))
                return (false, "Липсва Gemini API ключ в настройките.");

            try
            {
                using var memoryStream = new MemoryStream();
                await imageFile.CopyToAsync(memoryStream, cancellationToken);
                string base64Image = Convert.ToBase64String(memoryStream.ToArray());

                string prompt = $@"
Анализирай предоставеното изображение и прецени дали то съответства на кулинарната рецепта:
Име на рецептата: ""{recipeTitle}""
Съставки: ""{ingredients}""

Отговори в следния STRICT JSON формат (без допълнителен текст или ```json fences):
{{
  ""isMatch"": true или false,
  ""reason"": ""Кратко обяснение на български език защо снимката съответства или не съответства.""
}}";

                var requestBody = new
                {
                    contents = new[]
                    {
                new
                {
                    parts = new object[]
                    {
                        new { text = prompt },
                        new
                        {
                            inline_data = new
                            {
                                mime_type = string.IsNullOrWhiteSpace(imageFile.ContentType) ? "image/jpeg" : imageFile.ContentType,
                                data = base64Image
                            }
                        }
                    }
                }
            },
                    generationConfig = new { temperature = 0.1, responseMimeType = "application/json" }
                };

                var requestJson = JsonSerializer.Serialize(requestBody);

                var modelsToTry = new List<string> { GetConfiguredModel() };
                foreach (var fallback in FallbackModels)
                    if (!modelsToTry.Contains(fallback)) modelsToTry.Add(fallback);

                string responseText = string.Empty;
                int lastStatusCode = 0;
                string lastErrorBody = string.Empty;
                bool allOverloaded = true;

                foreach (var modelName in modelsToTry)
                {
                    var requestUri = new Uri(BuildEndpoint(modelName, apiKey), UriKind.Absolute);

                    using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
                    using var response = await _http.PostAsync(requestUri, content, cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        responseText = await response.Content.ReadAsStringAsync(cancellationToken);
                        allOverloaded = false;
                        break;
                    }

                    lastStatusCode = (int)response.StatusCode;
                    lastErrorBody = await response.Content.ReadAsStringAsync(cancellationToken);

                    if (IsOverloaded(response) || lastStatusCode == 404) continue;

                    allOverloaded = false;
                    break;
                }

                if (string.IsNullOrEmpty(responseText))
                {
                    if (allOverloaded)
                        return (false, "Всички ИИ модели са претоварени в момента. Опитайте отново след малко.");

                    return (false, $"Грешка от Gemini API (HTTP {lastStatusCode}): {lastErrorBody}");
                }

                string rawAnswer = ExtractText(responseText) ?? "";
                using var resultDoc = JsonDocument.Parse(CleanJson(rawAnswer));

                bool isMatch = resultDoc.RootElement.GetProperty("isMatch").GetBoolean();
                string reason = resultDoc.RootElement.GetProperty("reason").GetString() ?? "";

                return (isMatch, reason);
            }
            catch (Exception ex)
            {
                return (false, $"Грешка при анализ от ИИ: {ex.Message}");
            }
        }

        // =========================================================================
        // Помощни методи
        // =========================================================================

        private string GetConfiguredModel()
        {
            return string.IsNullOrWhiteSpace(_options.GeminiModel) ? DefaultModel : _options.GeminiModel.Trim();
        }

        // Чист URL - без квадратни скоби и markdown форматиране
        private static string BuildEndpoint(string modelName, string apiKey)
        {
            return $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";
        }

        private static bool IsOverloaded(HttpResponseMessage response)
        {
            var code = (int)response.StatusCode;
            return code == 429 || code == 503;
        }

        private static string? ExtractText(string responseText)
        {
            using var doc = JsonDocument.Parse(responseText);

            return doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();
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