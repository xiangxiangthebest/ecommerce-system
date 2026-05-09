using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using EcommerceSystem.Data;
using System.Security.Claims;
using EcommerceSystem.Models;
using EcommerceSystem.Observers;

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

        private async Task<Seller?> GetCurrentSellerAsync()
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            return await _context.Seller.FirstOrDefaultAsync(x => x.Email == email);
        }

        // Home / Dashboard
        public async Task<IActionResult> Home(string tab = "General")
        {
            var seller = await GetCurrentSellerAsync();
            if (seller == null) return RedirectToAction("Login", "Auth");

            // If seller is not approved, force them back to General tab
            if (!seller.IsApproved && tab != "General")
                tab = "General";

            ViewBag.ActiveTab   = tab;
            ViewBag.ShopName    = seller.ShopName;
            ViewBag.IsApproved  = seller.IsApproved;

            if (seller.IsApproved)
            {
                var products = await _context.Products
                    .Where(p => p.SellerId == seller.UserId)
                    .ToListAsync();
                ViewBag.Products = products;

                if (tab == "Order")
                {
                    var orders = await _context.Order
                        .Include(o => o.Customer)
                        .Where(o => o.SellerUserId == seller.UserId)
                        .OrderByDescending(o => o.OrderTime)
                        .ToListAsync();
                    ViewBag.Orders = orders;
                }
            }

            return View();
        }

        // Edit Product
        [HttpGet]
        public async Task<IActionResult> AddProduct()
        {
            var seller = await GetCurrentSellerAsync();
            if (seller == null) return RedirectToAction("Login", "Auth");

            // Block unapproved sellers
            if (!seller.IsApproved)
            {
                TempData["Error"] = "Your account is pending admin approval. You cannot add products yet.";
                return RedirectToAction("Home", new { tab = "General" });
            }

            ViewBag.ShopName = seller.ShopName;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AddProduct(Product model, IFormFile ImageFile, string actionType)
        {
            var seller = await GetCurrentSellerAsync();
            if (seller == null) return RedirectToAction("Login", "Auth");

            // Block unapproved sellers
            if (!seller.IsApproved)
            {
                TempData["Error"] = "Your account is pending admin approval. You cannot add products yet.";
                return RedirectToAction("Home", new { tab = "General" });
            }

            // 1. Seed categories if empty
            if (!await _context.Category.AnyAsync())
            {
                var initialCategories = new List<Category>
                {
                    new Category { Name = "Fashion/Apparel",          Description = "Clothing and accessories" },
                    new Category { Name = "Consumer Electronics",     Description = "Gadgets and devices" },
                    new Category { Name = "Food and Beverages",       Description = "Drinks and snacks" },
                    new Category { Name = "Beauty and Personal Care", Description = "Cosmetics and skin care" },
                    new Category { Name = "Home Improvement",         Description = "Tools and home decor" },
                    new Category { Name = "Other",                    Description = "Miscellaneous items" }
                };
                _context.Category.AddRange(initialCategories);
                await _context.SaveChangesAsync();
            }

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
                var fileName   = Guid.NewGuid().ToString() + Path.GetExtension(ImageFile.FileName);
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

        // Edit Product
        [HttpGet]
        public async Task<IActionResult> EditProduct(int id)
        {
            var seller = await GetCurrentSellerAsync();
            if (seller == null) return RedirectToAction("Login", "Auth");

            if (!seller.IsApproved)
            {
                TempData["Error"] = "Your account is pending admin approval.";
                return RedirectToAction("Home", new { tab = "General" });
            }

            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            ViewBag.ShopName = seller.ShopName;
            return View(product);
        }

        [HttpPost]
        public async Task<IActionResult> EditProduct(Product model, IFormFile ImageFile, string actionType)
        {
            var seller = await GetCurrentSellerAsync();
            if (seller == null) return RedirectToAction("Login", "Auth");

            if (!seller.IsApproved)
            {
                TempData["Error"] = "Your account is pending admin approval.";
                return RedirectToAction("Home", new { tab = "General" });
            }

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
                var fileName   = Guid.NewGuid().ToString() + Path.GetExtension(ImageFile.FileName);
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

        //  Delete Product
        [HttpPost]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var seller = await GetCurrentSellerAsync();
            if (seller == null) return RedirectToAction("Login", "Auth");

            if (!seller.IsApproved)
            {
                TempData["Error"] = "Your account is pending admin approval.";
                return RedirectToAction("Home", new { tab = "General" });
            }

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

        // ── Update Order Status ───────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> UpdateOrderStatus(int orderId, OrderStatus newStatus)
        {
            var seller = await GetCurrentSellerAsync();
            if (seller == null) return RedirectToAction("Login", "Auth");

            if (!seller.IsApproved)
            {
                TempData["Error"] = "Your account is pending admin approval.";
                return RedirectToAction("Home", new { tab = "General" });
            }

            // 1. Load order from SQLite DB
            var order = await _context.Order
                .Include(o => o.Customer)
                .Include(o => o.Seller)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null) return NotFound();

            // 2. Attach observers
            order.Attach(new CustomerDashboardObserver());
            order.Attach(new SellerDashboardObserver());
            order.Attach(new AdminPanelObserver());

            // 3. Update status — triggers NotifyObservers() automatically
            order.SetStatus(newStatus);

            // 4. Save to DB
            await _context.SaveChangesAsync();

            return RedirectToAction("Home", new { tab = "Orders" });
        }
    }
}