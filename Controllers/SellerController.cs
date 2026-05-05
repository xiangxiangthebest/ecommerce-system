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

        public async Task<IActionResult> Home(string tab = "General")
        {
            ViewBag.ActiveTab = tab;
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var seller = await _context.Seller.FirstOrDefaultAsync(x => x.Email == email);

            ViewBag.ShopName = seller?.ShopName ?? "Seller";

            if (seller != null)
            {
                var products = await _context.Products
                    .Where(p => p.SellerId == seller.UserId)
                    .ToListAsync();
                ViewBag.Products = products;
            }

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> AddProduct()
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var seller = await _context.Seller.FirstOrDefaultAsync(x => x.Email == email);
            ViewBag.ShopName = seller?.ShopName ?? "Seller";
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AddProduct(Product model, IFormFile ImageFile, string actionType)
        {
            // 1. Seed categories if empty
            if (!await _context.Category.AnyAsync())
            {
                var initialCategories = new List<Category>
                {
                    new Category { Name = "Fashion/Apparel",         Description = "Clothing and accessories" },
                    new Category { Name = "Consumer Electronics",    Description = "Gadgets and devices" },
                    new Category { Name = "Food and Beverages",      Description = "Drinks and snacks" },
                    new Category { Name = "Beauty and Personal Care", Description = "Cosmetics and skin care" },
                    new Category { Name = "Home Improvement",        Description = "Tools and home decor" },
                    new Category { Name = "Other",                   Description = "Miscellaneous items" }
                };
                _context.Category.AddRange(initialCategories);
                await _context.SaveChangesAsync();
            }

            // 2. Identify the seller
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var seller = await _context.Seller.FirstOrDefaultAsync(x => x.Email == email);

            if (seller == null)
                return RedirectToAction("Login", "Auth");

            model.SellerId = seller.UserId;

            // Ensure non-nullable string fields are never null
            model.Name        = model.Name        ?? string.Empty;
            model.Description = model.Description ?? string.Empty;
            model.SKU         = model.SKU         ?? string.Empty;

            // 3. Assign category
            var categoryName = Request.Form["Category"].ToString();
            var category = await _context.Category.FirstOrDefaultAsync(c => c.Name == categoryName);
            model.CategoryId = category != null
                ? category.CategoryId
                : (await _context.Category.FirstAsync()).CategoryId;

            // 4. Set draft flag
            model.IsDraft = actionType == "Draft";

            // 5. Handle image upload
            if (ImageFile != null && ImageFile.Length > 0)
            {
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(ImageFile.FileName);
                var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");
                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                var filePath = Path.Combine(folderPath, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                    await ImageFile.CopyToAsync(stream);

                model.ImagePath = "/images/" + fileName;
            }
            else
            {
                model.ImagePath = "/images/placeholder.png";
            }

            // 6. Save
            _context.Products.Add(model);
            await _context.SaveChangesAsync();

            return RedirectToAction("Home", new { tab = "Product" });
        }

        // ── Edit Product ──────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> EditProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var seller = await _context.Seller.FirstOrDefaultAsync(x => x.Email == email);
            ViewBag.ShopName = seller?.ShopName ?? "Seller";

            return View(product);
        }

        [HttpPost]
        public async Task<IActionResult> EditProduct(Product model, IFormFile ImageFile, string actionType)
        {
            var existing = await _context.Products.FindAsync(model.ProductId);
            if (existing == null) return NotFound();

            // Update fields
            existing.Name          = model.Name        ?? string.Empty;
            existing.Description   = model.Description ?? string.Empty;
            existing.SKU           = model.SKU         ?? string.Empty;
            existing.Price         = model.Price;
            existing.StockQuantity = model.StockQuantity;
            existing.IsDraft       = actionType == "Draft";

            // Update category
            var categoryName = Request.Form["Category"].ToString();
            var category = await _context.Category.FirstOrDefaultAsync(c => c.Name == categoryName);
            if (category != null)
                existing.CategoryId = category.CategoryId;

            // Update image only if a new one was uploaded
            if (ImageFile != null && ImageFile.Length > 0)
            {
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(ImageFile.FileName);
                var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");
                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                var filePath = Path.Combine(folderPath, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                    await ImageFile.CopyToAsync(stream);

                existing.ImagePath = "/images/" + fileName;
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Home", new { tab = "Product" });
        }

        // ── Delete Product ────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                if (!string.IsNullOrEmpty(product.ImagePath))
                {
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", product.ImagePath.TrimStart('/'));
                    if (System.IO.File.Exists(filePath))
                        System.IO.File.Delete(filePath);
                }

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Home", new { tab = "Product" });
        }
    }
}