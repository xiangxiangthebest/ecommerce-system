using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using EcommerceSystem.Data;
using System.Security.Claims;
using EcommerceSystem.Models;

namespace EcommerceSystem.Controllers
{
    [Authorize(Roles = "Seller")]
    public class SellerController : Controller
    {
        private readonly AppDbContext _context;

        public SellerController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string tab = "General")
        {
            ViewBag.ActiveTab = tab;
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var user = await _context.Users.FirstOrDefaultAsync(x => x.Email == email);

            ViewBag.ShopName = user?.ShopName ?? "Seller";

            if (user != null)
            {
                var products = await _context.Products
                    .Where(p => p.SellerId == user.UserId)
                    .ToListAsync();
                ViewBag.Products = products;
            }

            return View();
        }

        // GET: Just show the Add Product form
        [HttpGet]
        public async Task<IActionResult> AddProduct()
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var user = await _context.Users.FirstOrDefaultAsync(x => x.Email == email);
            ViewBag.ShopName = user?.ShopName ?? "Seller";
            return View();
        }

        // POST: Save the product to the database
        [HttpPost]
        public async Task<IActionResult> AddProduct(Product model, IFormFile ImageFile)
        {
            // Link product to the logged-in seller
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var user = await _context.Users.FirstOrDefaultAsync(x => x.Email == email);
            if (user != null) model.SellerId = user.UserId;

            // Handle image upload
            if (ImageFile != null && ImageFile.Length > 0)
            {
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(ImageFile.FileName);
                var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");

                // Create the folder if it doesn't exist
                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                var filePath = Path.Combine(folderPath, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await ImageFile.CopyToAsync(stream);
                }
                model.ImagePath = "/images/" + fileName;
            }
            else
            {
                // Use a default placeholder if no image uploaded
                model.ImagePath = "/images/placeholder.png";
            }

            _context.Products.Add(model);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", new { tab = "Product" });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                // Optional: Delete the physical file from wwwroot/images
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", product.ImagePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Index", new { tab = "Product" });
        }
    }
}