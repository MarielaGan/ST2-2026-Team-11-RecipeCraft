using Microsoft.AspNetCore.Mvc;
using RecipeCraft.Models;
using System.Diagnostics;

namespace RecipeCraft.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            // Ако потребителят вече е влязъл, го препращаме директно към рецептите
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Recipes");
            }

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
