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

        public SellerController(AppDbContext context)
        {
            _context = context;
        }

        private async Task<Seller?> GetCurrentSellerAsync()
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            return await _context.Seller.FirstOrDefaultAsync(x => x.Email == email);
        }

        private async Task<List<string>> SaveImagesAsync(List<IFormFile> files)
        {
            var paths = new List<string>();
            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            foreach (var file in files)
            {
                if (file == null || file.Length == 0) continue;
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                var filePath = Path.Combine(folderPath, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                    await file.CopyToAsync(stream);
                paths.Add("/images/" + fileName);
            }
            return paths;
        }

        // ── Safe int parser: handles Number/String/Double JSON values ──
        private static int SafeGetInt(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Number => element.TryGetInt32(out var i) ? i : (int)element.GetDouble(),
                JsonValueKind.String => int.TryParse(element.GetString(), out var s) ? s : 0,
                _ => 0
            };
        }

        // ── Sum all combination stocks from VariationCombosJson ──
        // Returns 0 if no combinations defined.
        private static int SumComboStock(string combosJson)
        {
            if (string.IsNullOrWhiteSpace(combosJson) || combosJson == "[]")
                return 0;
            try
            {
                var combos = JsonSerializer.Deserialize<List<JsonElement>>(combosJson);
                if (combos == null || combos.Count == 0) return 0;

                int total = 0;
                foreach (var c in combos)
                {
                    if (c.TryGetProperty("stock", out var s))
                        total += SafeGetInt(s);
                }
                return total;
            }
            catch
            {
                return 0;
            }
        }

        // ── Fallback: sum stocks from the old flat VariationsJson format ──
        // Used only for backward-compatibility with products saved before the combo system.
        private static int SumVariationStock(string variationsJson)
        {
            if (string.IsNullOrWhiteSpace(variationsJson) || variationsJson == "[]")
                return 0;
            try
            {
                var groups = JsonSerializer.Deserialize<List<JsonElement>>(variationsJson);
                if (groups == null) return 0;
                int total = 0;
                foreach (var g in groups)
                {
                    if (!g.TryGetProperty("values", out var vals)) continue;
                    foreach (var v in vals.EnumerateArray())
                    {
                        if (v.TryGetProperty("stock", out var s))
                            total += SafeGetInt(s);
                    }
                }
                return total;
            }
            catch { return 0; }
        }

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

        [HttpPost]
        public async Task<IActionResult> AddProduct(Product model, List<IFormFile> ImageFiles, string actionType)
        {
            var seller = await GetCurrentSellerAsync();
            if (seller == null) return RedirectToAction("Login", "Auth");

            if (!seller.IsApproved)
            {
                TempData["Error"] = "Your account is pending admin approval. You cannot add products yet.";
                return RedirectToAction("Home", new { tab = "General" });
            }

            bool isDraft = actionType == "Draft";

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

            // ── Read variation groups (names + option labels + optional images) ──
            model.VariationsJson = Request.Form["VariationsJson"].ToString();
            if (string.IsNullOrWhiteSpace(model.VariationsJson))
                model.VariationsJson = "[]";

            // ── Read variation combinations (stock per combo) ──
            model.VariationCombosJson = Request.Form["VariationCombosJson"].ToString();
            if (string.IsNullOrWhiteSpace(model.VariationCombosJson))
                model.VariationCombosJson = "[]";

            var categoryName = Request.Form["Category"].ToString();
            if (!string.IsNullOrEmpty(categoryName))
            {
                var category = await _context.Category.FirstOrDefaultAsync(c => c.Name == categoryName);
                if (category != null) model.CategoryId = category.CategoryId;
            }
            if (model.CategoryId == 0)
                model.CategoryId = (await _context.Category.FirstAsync()).CategoryId;

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

            // Process variation option images (strips base64, saves real files)
            model.VariationsJson = await ProcessVariationImagesAsync(model.VariationsJson, Request.Form.Files);

            // Auto-sum StockQuantity from combination stocks when combinations are present
            var comboStockSum = SumComboStock(model.VariationCombosJson);
            if (comboStockSum > 0)
                model.StockQuantity = comboStockSum;

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

            // ── Variation groups ──
            var variationsJson = Request.Form["VariationsJson"].ToString();
            existing.VariationsJson = string.IsNullOrWhiteSpace(variationsJson) ? "[]" : variationsJson;

            // ── Variation combinations ──
            var combosJson = Request.Form["VariationCombosJson"].ToString();
            existing.VariationCombosJson = string.IsNullOrWhiteSpace(combosJson) ? "[]" : combosJson;

            var categoryName = Request.Form["Category"].ToString();
            if (!string.IsNullOrEmpty(categoryName))
            {
                var category = await _context.Category.FirstOrDefaultAsync(c => c.Name == categoryName);
                if (category != null)
                    existing.CategoryId = category.CategoryId;
            }

            // Rebuild image list from slot assignment
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

            // Auto-sum StockQuantity from combination stocks when combinations are present
            var comboStockSum = SumComboStock(existing.VariationCombosJson);
            if (comboStockSum > 0)
                existing.StockQuantity = comboStockSum;

            TempData["Success"] = existing.IsDraft ? "Product saved as draft." : "Product updated successfully.";
            await _context.SaveChangesAsync();
            return RedirectToAction("Home", new { tab = "Product" });
        }

        // ── ProcessVariationImagesAsync ───────────────────────────────────────
        // Variation groups no longer carry stock — only label + imagePath.
        // Stock lives in VariationCombosJson.
        private async Task<string> ProcessVariationImagesAsync(string variationsJson, IFormFileCollection allFiles)
        {
            try
            {
                var groups = JsonSerializer.Deserialize<List<JsonElement>>(variationsJson);
                if (groups == null || groups.Count == 0) return variationsJson;

                var result = new List<object>();
                for (int gi = 0; gi < groups.Count; gi++)
                {
                    var g    = groups[gi];
                    var name = g.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    var vals = g.TryGetProperty("values", out var vs)
                        ? vs.EnumerateArray().ToList()
                        : new List<JsonElement>();

                    var newValues = new List<object>();
                    for (int vi = 0; vi < vals.Count; vi++)
                    {
                        var v     = vals[vi];
                        var label = v.TryGetProperty("label", out var l) ? l.GetString() ?? "" : "";

                        string existingImg = "";
                        if (v.TryGetProperty("imagePath", out var ip)) existingImg = ip.GetString() ?? "";
                        else if (v.TryGetProperty("image", out var img)) existingImg = img.GetString() ?? "";

                        // Discard blob/base64 preview URLs — only real server paths are stored
                        if (existingImg.StartsWith("data:")) existingImg = "";
                        if (existingImg.StartsWith("blob:"))  existingImg = "";

                        var fileKey = $"VarImg_{gi}_{vi}";
                        var file    = allFiles[fileKey];
                        string imagePath = existingImg;

                        if (file != null && file.Length > 0)
                        {
                            var saved = await SaveImagesAsync(new List<IFormFile> { file });
                            if (saved.Count > 0) imagePath = saved[0];
                        }

                        // NOTE: no 'stock' field here — stock is in combos
                        newValues.Add(new { label, imagePath });
                    }

                    result.Add(new { name, values = newValues });
                }

                return JsonSerializer.Serialize(result);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ProcessVariationImagesAsync] ERROR: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                return variationsJson;
            }
        }

        private List<string> BuildMergedImageList(string slotAssignmentJson, List<string> keptPaths,
                                                   List<string> newPaths, string existingImagePathsJson)
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