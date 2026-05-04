using Microsoft.AspNetCore.Mvc;

namespace YourProjectName.Controllers
{
    public class CustomerController : Controller
    {
        public IActionResult Home()
        {
            return View();
        }
    }
}