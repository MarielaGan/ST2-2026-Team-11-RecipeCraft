using Microsoft.AspNetCore.Mvc;
using ST2_2026_Team_10_RecipeCraft.Models;
using System.Diagnostics;

namespace ST2_2026_Team_10_RecipeCraft.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
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
