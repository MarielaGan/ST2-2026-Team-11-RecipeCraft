using Microsoft.AspNetCore.Mvc;
using RecipeCraft.Services;

namespace RecipeCraft.Controllers
{
    public class SettingsController : Controller
    {
        // GET: /Settings
        public IActionResult Index()
        {
            // Вземаме текущите настройки от Singleton инстанцията
            var settings = AppSettingsManager.Instance.Settings;
            return View(settings);
        }

        // POST: /Settings/Update
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Update(AppSettingsModel model)
        {
            // 1. Валидация през Singleton мениджъра
            var validationResults = AppSettingsManager.Instance.Validate(model);
            foreach (var result in validationResults)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Невалидни данни");
            }

            if (!ModelState.IsValid)
            {
                return View("Index", model);
            }

            // 2. Запазване в Singleton инстанцията и във файла appsettings_custom.json
            AppSettingsManager.Instance.Save(model);

            ViewBag.SuccessMessage = "Настройките бяха запазени успешно!";
            return View("Index", model);
        }
    }
}