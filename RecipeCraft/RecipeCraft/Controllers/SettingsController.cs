using Microsoft.AspNetCore.Mvc;
using ST2_2026_Team_10_RecipeCraft.Services;

namespace ST2_2026_Team_10_RecipeCraft.Controllers
{
    public class SettingsController : Controller
    {
        public IActionResult Index()
        {
            var settings = AppSettingsManager.Instance.Settings;
            return View(settings);
        }

        // Запази новите настройки (Validate & Save)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Index(AppSettingsModel model)
        {
            // Използваме метода Validate от Singleton-а
            var validationResults = AppSettingsManager.Instance.Validate(model);

            if (!ModelState.IsValid || validationResults.Count > 0)
            {
                foreach (var validationResult in validationResults)
                {
                    ModelState.AddModelError("", validationResult.ErrorMessage ?? "Невалидна стойност");
                }
                return View(model);
            }

            // Запазваме чрез Singleton инстанцията
            AppSettingsManager.Instance.Save(model);
            ViewBag.SuccessMessage = "Настройките бяха успешно запазени!";

            return View(model);
        }
    }

}
