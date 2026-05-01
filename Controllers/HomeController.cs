using Microsoft.AspNetCore.Mvc;

namespace EcommerceSystem.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}