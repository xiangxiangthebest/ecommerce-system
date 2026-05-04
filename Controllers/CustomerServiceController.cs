using Microsoft.AspNetCore.Mvc;

namespace YourProjectName.Controllers
{
    public class CustomerServiceController : Controller
    {
        public IActionResult Home()
        {
            return View();
        }
    }
}