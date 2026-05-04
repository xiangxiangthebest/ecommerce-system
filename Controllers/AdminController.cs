using Microsoft.AspNetCore.Mvc;

namespace YourProjectName.Controllers
{
    public class AdminController : Controller
    {
        public IActionResult Home()
        {
            return View();
        }
    }
}