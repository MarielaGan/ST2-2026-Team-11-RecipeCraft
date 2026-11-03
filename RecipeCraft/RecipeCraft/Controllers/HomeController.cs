using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeCraft.Data;
using RecipeCraft.Models;
using RecipeCraft.Services;
using System.Diagnostics;

namespace RecipeCraft.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        // Впръскваме DbContext, за да имаме достъп до рецептите в базата данни
        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Вземаме динамичната бройка от настройките (ако е 0 или по-малка, слагаме по подразбиране 3)
            int pageSize = AppSettingsManager.Instance.Settings.RecipesPerPage;
            if (pageSize <= 0) pageSize = 3;

            // Вземаме последните рецепти според зададеното ограничение
            var latestRecipes = await _context.Recipes
                .OrderByDescending(r => r.Id)
                .Take(pageSize)
                .ToListAsync();

            // Подаваме списъка с рецепти към View-то (Index.cshtml)
            return View(latestRecipes);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}