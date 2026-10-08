using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace ST2_2026_Team_10_RecipeCraft.Services
{
    public class AppSettingsModel
    {
        [Required(ErrorMessage = "Заглавието на сайта е задължително")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Заглавието трябва да е между 3 и 50 символа")]
        public string SiteTitle { get; set; } = "RecipeCraft";

        [Range(1, 100, ErrorMessage = "Рецептите на страница трябва да са между 1 и 100")]
        public int RecipesPerPage { get; set; } = 10;

        public bool EnableAiAssistant { get; set; } = true;

        [EmailAddress(ErrorMessage = "Невалиден имейл адрес за поддръжка")]
        public string AdminEmail { get; set; } = "admin@recipecraft.com";
    }

    /// <summary>
    /// DESIGN PATTERN: Singleton Pattern
    /// Управлява настройките на сайта (AppSettings) с възможност за Load, Save и Validate.
    /// </summary>
    public sealed class AppSettingsManager
    {
        private static AppSettingsManager? _instance;
        private static readonly object _lock = new object();
        private readonly string _filePath;

        public AppSettingsModel Settings { get; private set; }

        private AppSettingsManager()
        {
            _filePath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings_custom.json");
            Settings = new AppSettingsModel();
            Load(); // Зарежда настройките при първоначална инстанциация
        }

        public static AppSettingsManager Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new AppSettingsManager();
                    }
                    return _instance;
                }
            }
        }

        // 1. LOAD: Зареждане от JSON файл
        public void Load()
        {
            if (File.Exists(_filePath))
            {
                try
                {
                    string json = File.ReadAllText(_filePath);
                    var loadedSettings = JsonSerializer.Deserialize<AppSettingsModel>(json);
                    if (loadedSettings != null)
                    {
                        Settings = loadedSettings;
                    }
                }
                catch
                {
                    // При грешка се запазват настройките по подразбиране
                }
            }
        }

        // 2. SAVE: Запазване във файл
        public void Save(AppSettingsModel newSettings)
        {
            Settings = newSettings;
            string json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }

        // 3. VALIDATE: Валидация на новите настройки
        public List<ValidationResult> Validate(AppSettingsModel settingsToValidate)
        {
            var results = new List<ValidationResult>();
            var context = new ValidationContext(settingsToValidate);
            Validator.TryValidateObject(settingsToValidate, context, results, validateAllProperties: true);
            return results;
        }
    }
}

