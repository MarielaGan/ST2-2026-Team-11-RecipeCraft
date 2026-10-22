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
            // 1. Валидация на новите настройки през Singleton мениджъра
            var validationResults = AppSettingsManager.Instance.Validate(model);
            foreach (var error in validationResults)
            {
                foreach (var memberName in error.MemberNames)
                {
                    ModelState.AddModelError(memberName, error.ErrorMessage ?? "Невалидни данни.");
                }
            }

            if (!ModelState.IsValid)
            {
                return View("Index", model);
            }

            // 2. Запазване на настройките във файла през Singleton мениджъра
            AppSettingsManager.Instance.Save(model);

            // 3. Пренасочване към Index изгледа с рецептите
            return RedirectToAction("Index", "Recipes");
        }
    }
}