using EcommerceSystem.Data;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceSystem.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _context;

    public HomeController(AppDbContext context)
    {
        _context = context;
    }

    public IActionResult Index()
    {
        ViewBag.Category = _context.Category.ToList();
        var products = _context.Products.ToList();
        return View(products);
    }
}