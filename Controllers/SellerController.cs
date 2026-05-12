using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using EcommerceSystem.Data;
using System.Security.Claims;
using EcommerceSystem.Models;
using EcommerceSystem.Observers;
using System.Text.Json;

namespace EcommerceSystem.Controllers
{
    [Authorize(Roles = "Seller")]
    public class SellerController : Controller
    {
        private readonly AppDbContext _context; 

        // allow controller read and write the database
        public SellerController(AppDbContext context)
        {
            _context = context;
        } 

        // helper to get current logged-in seller based on the email 
        private async Task<Seller?> GetCurrentSellerAsync()
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            return await _context.Seller.FirstOrDefaultAsync(x => x.Email == email);
        }

        // helper to save uploaded images and return the paths 
        private async Task<List<string>> SaveImagesAsync(List<IFormFile> files)
        {
            var paths = new List<string>();
            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images"); // create a folder name as wwwroot/images(if not exist) else continue 
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            foreach (var file in files) // handle multiple file one by one 
            {
                if (file == null || file.Length == 0) continue; 
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName); // change the file name into random text from cake.png into ajdh1iu171.png
                var filePath = Path.Combine(folderPath, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                    await file.CopyToAsync(stream); // then save to folder 
                paths.Add("/images/" + fileName); //show the final (ajdh1iu171.png) into AddProduct/ EditProduct
            }
            return paths;
        }

        // if seller is not approve by admin, they can only see/ stay at general page, cnt move to products or orders page
        public async Task<IActionResult> Home(string tab = "General")
        {
            var seller = await GetCurrentSellerAsync();
            if (seller == null) return RedirectToAction("Login", "Auth");

            if (!seller.IsApproved && tab != "General")
                tab = "General";

            ViewBag.ActiveTab  = tab;
            ViewBag.ShopName   = seller.ShopName;
            ViewBag.IsApproved = seller.IsApproved;

            if (seller.IsApproved)
            {
                var products = await _context.Products
                    .Include(p => p.Category)
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

        // sellet that not yet approve by admin couldnt access to the + Add New Product
        // and show the message Your account is pending admin approval. You cannot add products yet.
        [HttpGet]
        public async Task<IActionResult> AddProduct()
        {
            var seller = await GetCurrentSellerAsync();
            if (seller == null) return RedirectToAction("Login", "Auth");

            if (!seller.IsApproved)
            {
                TempData["Error"] = "Your account is pending admin approval. You cannot add products yet.";
                return RedirectToAction("Home", new { tab = "General" });
            }

            ViewBag.ShopName = seller.ShopName;
            return View();
        }

        // add product 
        [HttpPost]
        public async Task<IActionResult> AddProduct(Product model, List<IFormFile> ImageFiles, string actionType)
        {
            // 1. verify seller identity and approval status 
            var seller = await GetCurrentSellerAsync();
            if (seller == null) return RedirectToAction("Login", "Auth");

            if (!seller.IsApproved)
            {
                TempData["Error"] = "Your account is pending admin approval. You cannot add products yet.";
                return RedirectToAction("Home", new { tab = "General" });
            }
             
            // 2. seller can save as draft without filling all the fields (empty the Description, SKu.. )
            bool isDraft = actionType == "Draft"; 

            // 3. but if seller want to Save & Publish, seller need to fill ALL the fields, else show error and remain the same page
            if (!isDraft)
            {
                if (string.IsNullOrWhiteSpace(model.Name))
                    ModelState.AddModelError("Name", "Product name is required to publish.");
                if (model.Price <= 0)
                    ModelState.AddModelError("Price", "Price must be greater than 0 to publish.");
                if (!ModelState.IsValid)
                {
                    ViewBag.ShopName = seller.ShopName;
                    return View(model);
                }
            }

            // check if there is category in database, if no, add back these category in database to prevent null 
            if (!await _context.Category.AnyAsync())
            {
                _context.Category.AddRange(new List<Category>
                {
                    new Category { Name = "Fashion/Apparel",          Description = "Clothing and accessories" },
                    new Category { Name = "Consumer Electronics",     Description = "Gadgets and devices" },
                    new Category { Name = "Food and Beverages",       Description = "Drinks and snacks" },
                    new Category { Name = "Beauty and Personal Care", Description = "Cosmetics and skin care" },
                    new Category { Name = "Home Improvement",         Description = "Tools and home decor" },
                    new Category { Name = "Other",                    Description = "Miscellaneous items" }
                });
                await _context.SaveChangesAsync();
            }

            model.SellerId    = seller.UserId;
            model.Name        = model.Name        ?? string.Empty;
            model.Description = model.Description ?? string.Empty;
            model.SKU         = model.SKU         ?? string.Empty;
            model.IsDraft     = isDraft;

            // 1. Get variation data and ensure it's not null
            model.VariationsJson = Request.Form["VariationsJson"].ToString();
            if (string.IsNullOrWhiteSpace(model.VariationsJson))
                model.VariationsJson = "[]";

            // 2. Link the product to the correct Category ID
            var categoryName = Request.Form["Category"].ToString();
            if (!string.IsNullOrEmpty(categoryName))
            {
                var category = await _context.Category.FirstOrDefaultAsync(c => c.Name == categoryName);
                if (category != null) model.CategoryId = category.CategoryId;
            }
            if (model.CategoryId == 0)
                model.CategoryId = (await _context.Category.FirstAsync()).CategoryId;

            // 3. Save uploaded images and set the main cover photo
            var savedPaths = await SaveImagesAsync(ImageFiles ?? new List<IFormFile>());

            if (savedPaths.Count > 0)
            {
                model.ImagePath      = savedPaths[0];
                model.ImagePathsJson = JsonSerializer.Serialize(savedPaths);
            }
            else
            {
                model.ImagePath      = "/images/placeholder.png";
                model.ImagePathsJson = "[]";
            }

            model.VariationsJson = await ProcessVariationImagesAsync(model.VariationsJson, Request.Form.Files);

            _context.Products.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = isDraft ? "Product saved as draft." : "Product published successfully.";
            return RedirectToAction("Home", new { tab = "Product" });
        }

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

            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null) return NotFound();

            ViewBag.ShopName = seller.ShopName;
            return View(product);
        }

        [HttpPost]
        public async Task<IActionResult> EditProduct(Product model, List<IFormFile> ImageFiles, string actionType)
        {
            var seller = await GetCurrentSellerAsync();
            if (seller == null) return RedirectToAction("Login", "Auth");

            if (!seller.IsApproved)
            {
                TempData["Error"] = "Your account is pending admin approval.";
                return RedirectToAction("Home", new { tab = "General" });
            }

            var existing = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.ProductId == model.ProductId);

            if (existing == null) return NotFound();

            existing.Name          = model.Name        ?? string.Empty;
            existing.Description   = model.Description ?? string.Empty;
            existing.SKU           = model.SKU         ?? string.Empty;
            existing.Price         = model.Price;
            existing.StockQuantity = model.StockQuantity;
            existing.IsDraft       = actionType == "Draft";

            var variationsJson = Request.Form["VariationsJson"].ToString();
            existing.VariationsJson = string.IsNullOrWhiteSpace(variationsJson) ? "[]" : variationsJson;

            var categoryName = Request.Form["Category"].ToString();
            if (!string.IsNullOrEmpty(categoryName))
            {
                var category = await _context.Category.FirstOrDefaultAsync(c => c.Name == categoryName);
                if (category != null)
                    existing.CategoryId = category.CategoryId;
            }

            var existingOrderJson = Request.Form["ExistingImageOrder"].ToString();
            var keptPaths = new List<string>();
            if (!string.IsNullOrWhiteSpace(existingOrderJson))
            {
                try { keptPaths = JsonSerializer.Deserialize<List<string>>(existingOrderJson) ?? new List<string>(); }
                catch { keptPaths = new List<string>(); }
            }

            var newPaths = await SaveImagesAsync(ImageFiles ?? new List<IFormFile>());

            var slotAssignmentJson = Request.Form["SlotAssignment"].ToString();
            var mergedPaths = BuildMergedImageList(slotAssignmentJson, keptPaths, newPaths, existing.ImagePathsJson);

            if (mergedPaths.Count > 0)
            {
                existing.ImagePath      = mergedPaths[0];
                existing.ImagePathsJson = JsonSerializer.Serialize(mergedPaths);
            }

            existing.VariationsJson = await ProcessVariationImagesAsync(existing.VariationsJson, Request.Form.Files);

            TempData["Success"] = existing.IsDraft ? "Product saved as draft." : "Product updated successfully.";
            await _context.SaveChangesAsync();
            return RedirectToAction("Home", new { tab = "Product" });
        }

        private async Task<string> ProcessVariationImagesAsync(string variationsJson, IFormFileCollection allFiles)
        {
            try
            {
                var groups = JsonSerializer.Deserialize<List<JsonElement>>(variationsJson);
                if (groups == null || groups.Count == 0) return variationsJson;

                var result = new List<object>();
                for (int gi = 0; gi < groups.Count; gi++)
                {
                    var g = groups[gi];
                    var name = g.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    var values = g.TryGetProperty("values", out var vs)
                        ? vs.EnumerateArray().ToList()
                        : new List<JsonElement>();

                    var newValues = new List<object>();
                    for (int vi = 0; vi < values.Count; vi++)
                    {
                        var v = values[vi];
                        var label = v.TryGetProperty("label", out var l) ? l.GetString() ?? "" : "";
                        var stock = v.TryGetProperty("stock", out var s) ? s.GetInt32() : 0;
                        var existingImg = v.TryGetProperty("imagePath", out var ip) ? ip.GetString() ?? "" : "";

                        // Check if a new file was uploaded for this variation value
                        var fileKey = $"VarImg_{gi}_{vi}";
                        var file = allFiles[fileKey];
                        string imagePath = existingImg;

                        if (file != null && file.Length > 0)
                        {
                            var saved = await SaveImagesAsync(new List<IFormFile> { file });
                            if (saved.Count > 0) imagePath = saved[0];
                        }

                        newValues.Add(new { label, stock, imagePath });
                    }

                    result.Add(new { name, values = newValues });
                }

                return JsonSerializer.Serialize(result);
            }
            catch
            {
                return variationsJson;
            }
        }

        private List<string> BuildMergedImageList(string slotAssignmentJson, List<string> keptPaths, List<string> newPaths, string existingImagePathsJson)
        {
            if (!string.IsNullOrWhiteSpace(slotAssignmentJson))
            {
                try
                {
                    var slots = JsonSerializer.Deserialize<List<JsonElement>>(slotAssignmentJson);
                    if (slots != null && slots.Count > 0)
                    {
                        var merged = new List<string>();
                        foreach (var slot in slots)
                        {
                            var source = slot.TryGetProperty("source", out var src) ? src.GetString() : "";
                            if (source == "existing")
                            {
                                var path = slot.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "";
                                if (!string.IsNullOrEmpty(path)) merged.Add(path);
                            }
                            else if (source == "new")
                            {
                                var idx = slot.TryGetProperty("index", out var i) ? i.GetInt32() : -1;
                                if (idx >= 0 && idx < newPaths.Count) merged.Add(newPaths[idx]);
                            }
                        }
                        if (merged.Count > 0) return merged;
                    }
                }
                catch { }
            }

            var result = new List<string>(keptPaths);
            result.AddRange(newPaths);

            if (result.Count == 0)
            {
                try
                {
                    var original = JsonSerializer.Deserialize<List<string>>(existingImagePathsJson);
                    if (original != null) return original;
                }
                catch { }
            }

            return result;
        }

        // completely remove the product(images, recorded information) from database once the seller decide to Delete 
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
                var allPaths = new List<string>();
                if (!string.IsNullOrEmpty(product.ImagePathsJson))
                {
                    try { allPaths = JsonSerializer.Deserialize<List<string>>(product.ImagePathsJson) ?? new List<string>(); }
                    catch { }
                }
                if (allPaths.Count == 0 && !string.IsNullOrEmpty(product.ImagePath))
                    allPaths.Add(product.ImagePath);

                foreach (var imgPath in allPaths)
                {
                    if (string.IsNullOrEmpty(imgPath) || imgPath == "/images/placeholder.png") continue;
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", imgPath.TrimStart('/'));
                    if (System.IO.File.Exists(filePath))
                        System.IO.File.Delete(filePath);
                }

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Home", new { tab = "Product" });
        }

        // update the order status in the admin and relavant customer and seller  (Observer Design Pattern)
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

            var order = await _context.Order
                .Include(o => o.Customer)
                .Include(o => o.Seller)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null) return NotFound();

            order.Attach(new CustomerDashboardObserver());
            order.Attach(new SellerDashboardObserver());
            order.Attach(new AdminPanelObserver());
            order.SetStatus(newStatus);

            await _context.SaveChangesAsync();
            return RedirectToAction("Home", new { tab = "Orders" });
        }
    }
}
