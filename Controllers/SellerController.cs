using Microsoft.AspNetCore.Mvc;

namespace YourProjectName.Controllers
{
    public class SellerController : Controller
    {
        public IActionResult Home()
        {
            return View();
        }
    }
}