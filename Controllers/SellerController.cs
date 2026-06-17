using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using EcommerceSystem.Data;
using System.Security.Claims;
using EcommerceSystem.Models;
using EcommerceSystem.Interfaces;
using EcommerceSystem.Observers;
using System.Text.Json;

namespace EcommerceSystem.Controllers
{
    [Authorize(Roles = "Seller")]
    public class SellerController : Controller
    {
        private readonly AppDbContext _context;
        private readonly INotificationService _notificationService;


        public SellerController(AppDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
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

            var currentUserId = int.Parse(
                User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0"
            );

            if (!seller.IsApproved && tab != "General" && tab != "Profile")
                tab = "General";

            ViewBag.ActiveTab = tab;
            ViewBag.ShopName = seller.ShopName;
            ViewBag.IsApproved = seller.IsApproved;

            // var requests = await _context.Request.ToListAsync();
            // ViewBag.Requests = requests;

            var notifications = await _notificationService.GetForUserAsync(seller.UserId);
            ViewBag.UnreadNotificationCount = notifications?.Count(n => !n.IsRead) ?? 0;

            if (!seller.IsApproved)
                return View();

            // ─────────────────────────────────────────────
            // PRODUCTS
            // ─────────────────────────────────────────────
            var products = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.SellerId == seller.UserId)
                .ToListAsync();

            ViewBag.Products = products;
            ViewBag.BannedProductCount = products.Count(p => p.IsDeleted);

            var activeProducts = products.Where(p => !p.IsDeleted && !p.IsDraft).ToList();
            ViewBag.ActiveProductCount = activeProducts.Count;

            ViewBag.TotalRevenuePotential = activeProducts
                .Sum(p => p.Price * p.StockQuantity);

            // ── Revenue Calculation ───────────────────────────────────────────────
            // Revenue = sum of what customers actually paid (Order.TotalAmount,
            // which already excludes SST/shipping). NO deduction for refunds —
            // this is a pure total of customer payments, full stop.

            var deliveredOrders = await _context.Order
                .Include(o => o.OrderItems)
                .Where(o => o.SellerUserId == seller.UserId
                        && (o.CurrentStatus == OrderStatus.DELIVERED
                        || o.CurrentStatus == OrderStatus.RECEIVED
                        || o.CurrentStatus == OrderStatus.RETURN_REFUND
                        || o.CurrentStatus == OrderStatus.RETURN_REFUND_REQUESTED
                        || o.CurrentStatus == OrderStatus.RETURN_REFUND_REJECTED
                        || o.CurrentStatus == OrderStatus.REFUND))
                .ToListAsync();

                var approvedRefund  = _context.Request
                .Where(r => r.Status == "Approved")
                .ToList()
                .Sum(r => r.ApprovedRefundAmount ?? 0);

            decimal totalRevenue =
                deliveredOrders.Sum(o => o.TotalAmount)
                - approvedRefund;

                // ── Load ALL requests for this seller's orders (still needed elsewhere
                // on this page, e.g. after-sale request lookups) ─────────────────────
                var deliveredOrderIds = deliveredOrders.Select(o => o.OrderId).ToList();
                var allRequests = await _context.Request
                    .Where(r => deliveredOrderIds.Contains(r.OrderId))
                    .ToListAsync();

                ViewBag.Requests = allRequests;

            ViewBag.TotalProfit = totalRevenue; // kept ViewBag key name so the view doesn't need changes

            // ─────────────────────────────────────────────
            // TAB: ORDER
            // ─────────────────────────────────────────────
            if (tab == "Order")
            {

                var orders = await _context.Order
                        .Include(o => o.Customer)
                        .Include(o => o.OrderItems)
                            .ThenInclude(oi => oi.Product)
                        .Where(o => o.SellerUserId == seller.UserId)
                        .OrderByDescending(o => o.OrderTime)
                        .ToListAsync();
                ViewBag.Orders = orders; 
                    
                ViewBag.Orders = orders
                    .OrderByDescending(o => o.OrderTime)
                    .ToList();

                // Re-fetch requests scoped to ALL orders for the Order tab display
                var allOrderIds = orders.Select(o => o.OrderId).ToList();
                ViewBag.Requests = await _context.Request
                    .Where(r => allOrderIds.Contains(r.OrderId))
                    .ToListAsync();
            }

            // ─────────────────────────────────────────────
            // TAB: PROFILE
            // ─────────────────────────────────────────────
            if (tab == "Profile")
            {
                ViewBag.ProfileSeller = seller;
            }

            // ─────────────────────────────────────────────
            // TAB: CHAT
            // ─────────────────────────────────────────────
            if (tab == "Chat")
            {
                var chatList = await _context.ChatRoom
                    .Where(r => r.SellerId == currentUserId)
                    .Select(r => new EcommerceSystem.ViewModels.ChatBoxListMV
                    {
                        ChatRoomId = r.ChatRoomId,
                        CustomerName = r.Customer != null ? r.Customer.FullName : "Unknown Customer",
                        LastMessage = r.Messages
                            .OrderByDescending(m => m.SentAt)
                            .Select(m => m.MessageText)
                            .FirstOrDefault() ?? "",
                        LastMessageTime = r.Messages
                            .OrderByDescending(m => m.SentAt)
                            .Select(m => m.SentAt)
                            .FirstOrDefault(),
                        UnreadCount = r.Messages.Count(m => !m.IsRead && m.SenderId != currentUserId)
                    })
                    .OrderByDescending(x => x.LastMessageTime)
                    .ToListAsync();

                return View("Home", chatList);
            }

            // ─────────────────────────────────────────────
            // TAB: REVIEWS
            // ─────────────────────────────────────────────
            if (tab == "Reviews")
            {
                var productIds = products.Select(p => p.ProductId).ToList();

                var reviews = await _context.Reviews
                    .Include(r => r.OrderItem!)
                        .ThenInclude(oi => oi.Order!)
                            .ThenInclude(o => o.Customer)
                    .Include(r => r.OrderItem!)
                        .ThenInclude(oi => oi.Order!)
                            .ThenInclude(o => o.OrderItems)
                    .Where(r => productIds.Contains(r.ProductId))
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync();

                ViewBag.ReviewsByProduct = reviews
                    .GroupBy(r => r.ProductId)
                    .ToDictionary(g => g.Key, g => g.ToList());
            }

            return View();
        }

        // public async Task<IActionResult> Home(string tab = "General")
        // {
        //     var seller = await GetCurrentSellerAsync();
        //     if (seller == null) return RedirectToAction("Login", "Auth");
        //     var currentUserId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");

        //     if (!seller.IsApproved && tab != "General" && tab != "Profile")
        //         tab = "General";

        //     ViewBag.ActiveTab = tab;
        //     ViewBag.ShopName = seller.ShopName;
        //     ViewBag.IsApproved = seller.IsApproved;

        //     var notifications = await _notificationService.GetForUserAsync(seller.UserId);
        //     ViewBag.UnreadNotificationCount = notifications?.Count(n => !n.IsRead) ?? 0;

        //     if (seller.IsApproved)
        //     {
        //         var products = await _context.Products
        //             .Include(p => p.Category)
        //             .Where(p => p.SellerId == seller.UserId)
        //             .ToListAsync();

        //         ViewBag.Products = products;
        //         ViewBag.BannedProductCount = products.Count(p => p.IsDeleted);

        //         // ── Active products for revenue potential ──
        //         var activeProducts = products.Where(p => !p.IsDeleted && !p.IsDraft).ToList();
        //         ViewBag.ActiveProductCount = activeProducts.Count;

        //         // ── Revenue Potential = remaining stock × selling price ──
        //         ViewBag.TotalRevenuePotential = activeProducts
        //             .Sum(p => p.Price * p.StockQuantity);

        //         // ── Actual Profit = delivered orders (profit margin per unit sold) ──
        //         //    minus refunded amounts (approved returns only)
        //         var deliveredOrders = await _context.Order
        //             .Include(o => o.OrderItems)
        //             .Where(o =>
        //                 o.SellerUserId == seller.UserId &&
        //                 (o.CurrentStatus == OrderStatus.DELIVERED
        //                 || o.CurrentStatus == OrderStatus.RECEIVED))
        //             .ToListAsync();

        //         double actualProfit = 0;

        //         foreach (var order in deliveredOrders)
        //         {
        //             foreach (var item in order.OrderItems)
        //             {
        //                 // Find the original cost price of this product
        //                 var product = products.FirstOrDefault(p => p.ProductId == item.ProductId);
        //                 var costPrice = product != null && product.OriginalPrice > 0
        //                     ? product.OriginalPrice
        //                     : (double)item.Price; // fallback: no margin known

        //                 var margin = (double)item.Price - costPrice;
        //                 if (margin > 0)
        //                     actualProfit += margin * item.Quantity;
        //             }

        //             // Subtract refunded profit if return was approved
        //             if (order.ReturnApprovedAt.HasValue)
        //             {
        //                 foreach (var item in order.OrderItems)
        //                 {
        //                     var product = products.FirstOrDefault(p => p.ProductId == item.ProductId);
        //                     var costPrice = product != null && product.OriginalPrice > 0
        //                         ? product.OriginalPrice
        //                         : (double)item.Price;

        //                     var margin = (double)item.Price - costPrice;
        //                     if (margin > 0)
        //                         actualProfit -= margin * item.Quantity;
        //                 }
        //             }
        //         }

        //         ViewBag.TotalProfit = (decimal)actualProfit;
        //         if (tab == "Order")
        //         {
        //             var orders = await _context.Order
        //                 .Include(o => o.Customer)
        //                 .Include(o => o.OrderItems)
        //                     .ThenInclude(oi => oi.Product)
        //                 .Where(o => o.SellerUserId == seller.UserId)
        //                 .OrderByDescending(o => o.OrderTime)
        //                 .ToListAsync();
        //             ViewBag.Orders = orders;                                   
        //         }
        //         if (tab == "Profile")
        //             {
        //                 ViewBag.ProfileSeller = seller;
        //             }            

        //         if (tab == "Chat")
        //         {
        //             // 从数据库查询该卖家的所有聊天盒子列表 (这里复用你原本写在 ChatController.SellerInbox 里的查询语句)
        //             var chatList = await _context.ChatRoom
        //                 .Where(r => r.SellerId == currentUserId)
        //                 .Select(r => new EcommerceSystem.ViewModels.ChatBoxListMV
        //                 {
        //                     ChatRoomId = r.ChatRoomId,
        //                     CustomerName = r.Customer != null ? r.Customer.FullName : "Unknown Customer",
        //                     LastMessage = r.Messages.OrderByDescending(m => m.SentAt).Select(m => m.MessageText).FirstOrDefault() ?? "",
        //                     LastMessageTime = r.Messages.OrderByDescending(m => m.SentAt).Select(m => m.SentAt).FirstOrDefault(),
        //                     UnreadCount = r.Messages.Count(m => !m.IsRead && m.SenderId != currentUserId)
        //                 })
        //                 .OrderByDescending(x => x.LastMessageTime)
        //                 .ToListAsync();

        //             // 💡 返回的是卖家主视图，但带上了聊天列表数据模型
        //             return View("Home", chatList); 
        //         }
        //     }
        //     return View();
        // }

        // ─────────────────────────────────────────────────────────────────────
        // SELLER PROFILE — Update Address
        //
        // Only the Address field is editable.
        // Email, ContactNumber, TINNumber, and FullName are read-only and are
        // never bound from the form — they can only be changed by an admin.
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAddress(string address)
        {
            var seller = await GetCurrentSellerAsync();
            if (seller == null) return RedirectToAction("Login", "Auth");

            if (string.IsNullOrWhiteSpace(address))
            {
                TempData["ProfileError"] = "Address cannot be empty.";
                return RedirectToAction("Home", new { tab = "Profile" });
            }

            seller.PickupAddress = address.Trim();
            await _context.SaveChangesAsync();

            TempData["ProfileSuccess"] = "Your address has been updated successfully.";
            return RedirectToAction("Home", new { tab = "Profile" });
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
                var publishErrors = new List<string>();
                if (string.IsNullOrWhiteSpace(model.Name))
                    publishErrors.Add("Product name is required to publish.");
                if (model.Price <= 0)
                    publishErrors.Add("Price must be greater than 0 to publish.");

                if (publishErrors.Any())
                {
                    ViewBag.ShopName = seller.ShopName;
                    ViewBag.PublishErrors = publishErrors;
                    return View(model);
                }
            }

            if (model.OriginalPrice <= 0)
            {
                model.OriginalPrice = model.Price;
            }

            if (model.OriginalPrice > model.Price)
            {
                model.DiscountPercentage =
                    Math.Round(
                        ((model.OriginalPrice - model.Price)
                        / model.OriginalPrice) * 100,
                        0
                    );
            }
            else
            {
                model.DiscountPercentage = 0;
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

            model.SellerId = seller.UserId;
            model.Name = model.Name ?? string.Empty;
            model.Description = model.Description ?? string.Empty;
            model.SKU = model.SKU ?? string.Empty;
            model.IsDraft = isDraft;

            model.VariationsJson = Request.Form["VariationsJson"].ToString();
            if (string.IsNullOrWhiteSpace(model.VariationsJson))
                model.VariationsJson = "[]";

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
            var slotAssignmentJson = Request.Form["SlotAssignment"].ToString();
            var mergedPaths = BuildMergedImageList(slotAssignmentJson, new List<string>(), savedPaths, "[]");

            if (mergedPaths.Count > 0)
            {
                model.ImagePath = mergedPaths[0];
                model.ImagePathsJson = JsonSerializer.Serialize(mergedPaths);
            }
            else
            {
                model.ImagePath = "/images/placeholder.png";
                model.ImagePathsJson = "[]";
            }

            model.VariationsJson = await ProcessVariationImagesAsync(model.VariationsJson, Request.Form.Files);

            var comboStockSum = SumComboStock(model.VariationCombosJson);
            if (comboStockSum > 0)
                model.StockQuantity = comboStockSum;

            _context.Products.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = isDraft ? "Product saved as draft." : "Product published successfully.";
            return RedirectToAction("Home", new { tab = "Product", subtab = isDraft ? "draft" : "active" });
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

            if (product.IsDeleted)
            {
                TempData["Error"] = "This product has been removed by admin and cannot be edited.";
                return RedirectToAction("Home", new { tab = "Product", subtab = "banned" });
            }

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

            existing.Name = model.Name ?? string.Empty;
            existing.Description = model.Description ?? string.Empty;
            existing.SKU = model.SKU ?? string.Empty;
            existing.Price = model.Price;
            existing.StockQuantity = model.StockQuantity;
            existing.IsDraft = actionType == "Draft";
            existing.OriginalPrice = model.OriginalPrice;

            if (existing.OriginalPrice <= 0)
            {
                existing.OriginalPrice = existing.Price;
            }

            if (existing.OriginalPrice > existing.Price)
            {
                existing.DiscountPercentage =
                    Math.Round(
                        ((existing.OriginalPrice - existing.Price)
                        / existing.OriginalPrice) * 100,
                        0
                    );
            }
            else
            {
                existing.DiscountPercentage = 0;
            }

            var variationsJson = Request.Form["VariationsJson"].ToString();
            existing.VariationsJson = string.IsNullOrWhiteSpace(variationsJson) ? "[]" : variationsJson;

            var combosJson = Request.Form["VariationCombosJson"].ToString();
            existing.VariationCombosJson = string.IsNullOrWhiteSpace(combosJson) ? "[]" : combosJson;

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
                existing.ImagePath = mergedPaths[0];
                existing.ImagePathsJson = JsonSerializer.Serialize(mergedPaths);
            }

            existing.VariationsJson = await ProcessVariationImagesAsync(existing.VariationsJson, Request.Form.Files);

            var comboStockSum = SumComboStock(existing.VariationCombosJson);
            if (comboStockSum > 0)
                existing.StockQuantity = comboStockSum;

            TempData["Success"] = existing.IsDraft ? "Product saved as draft." : "Product updated successfully.";
            await _context.SaveChangesAsync();
            return RedirectToAction("Home", new { tab = "Product", subtab = existing.IsDraft ? "draft" : "active" });
        }

        // ── ProcessVariationImagesAsync ───────────────────────────────────────
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
                    var vals = g.TryGetProperty("values", out var vs)
                        ? vs.EnumerateArray().ToList()
                        : new List<JsonElement>();

                    var newValues = new List<object>();
                    for (int vi = 0; vi < vals.Count; vi++)
                    {
                        var v = vals[vi];
                        var label = v.TryGetProperty("label", out var l) ? l.GetString() ?? "" : "";

                        string existingImg = "";
                        if (v.TryGetProperty("imagePath", out var ip)) existingImg = ip.GetString() ?? "";
                        else if (v.TryGetProperty("image", out var img)) existingImg = img.GetString() ?? "";

                        if (existingImg.StartsWith("data:")) existingImg = "";
                        if (existingImg.StartsWith("blob:")) existingImg = "";

                        var fileKey = $"VarImg_{gi}_{vi}";
                        var file = allFiles[fileKey];
                        string imagePath = existingImg;

                        if (file != null && file.Length > 0)
                        {
                            var saved = await SaveImagesAsync(new List<IFormFile> { file });
                            if (saved.Count > 0) imagePath = saved[0];
                        }

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

        private static string RestoreComboStock(string combosJson, string selectedVariationsJson, int quantity)
        {
            if (string.IsNullOrWhiteSpace(combosJson) || combosJson == "[]") return combosJson;
            try
            {
                var selected = JsonSerializer.Deserialize<Dictionary<string, string>>(selectedVariationsJson)
                            ?? new Dictionary<string, string>();
                if (selected.Count == 0) return combosJson;

                var selectedValues = new HashSet<string>(selected.Values, StringComparer.OrdinalIgnoreCase);
                var combos = JsonSerializer.Deserialize<List<JsonElement>>(combosJson);
                if (combos == null) return combosJson;

                var updated = new List<object>();
                foreach (var combo in combos)
                {
                    var keys = combo.TryGetProperty("keys", out var k)
                        ? k.EnumerateArray().Select(x => x.GetString() ?? "").ToList()
                        : new List<string>();
                    int stock = combo.TryGetProperty("stock", out var s)
                        ? (s.ValueKind == JsonValueKind.Number ? s.GetInt32() : int.Parse(s.GetString() ?? "0"))
                        : 0;

                    bool isMatch = keys.Count > 0 && keys.All(key => selectedValues.Contains(key));
                    if (isMatch) stock += quantity;

                    updated.Add(new { keys, stock });
                }
                return JsonSerializer.Serialize(updated);
            }
            catch { return combosJson; }
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
                // Soft delete — keeps the record visible in Banned tab
                product.IsDeleted = true;
                product.DeletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Home", new { tab = "Product", subtab = "active" });
        }

        // ─────────────────────────────────────────────────────────────────────
        // UPDATE ORDER STATUS
        // Only the seller can push statuses that belong to their workflow:
        // ─────────────────────────────────────────────────────────────────────
        // UPDATE ORDER STATUS  (Seller-side only)
        //
        // The seller can only push orders forward along their own path:
        //   PENDING   → PREPARING   (accept the order)
        //   PREPARING → SHIPPED     (hand to courier)
        //   SHIPPED   → DELIVERED   (courier delivered to address)
        //
        // The following are BLOCKED for sellers:
        //   CANCELED      — customer-only action (only while PENDING)
        //   RECEIVED      — triggered by customer "Received" button or AutoReceiveOrdersJob
        //   RETURN_REFUND — initiated by customer only
        //
        // Any attempt to submit a status outside SellerAllowedTransitions is
        // rejected server-side even if someone bypasses the UI.
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateOrderStatus(int orderId, OrderStatus newStatus)
        {
            var seller = await GetCurrentSellerAsync();
            if (seller == null) return RedirectToAction("Login", "Auth");

            if (!seller.IsApproved)
            {
                TempData["OrderError"] = "Your account is pending admin approval.";
                return RedirectToAction("Home", new { tab = "General" });
            }

            // Load order and verify it belongs to this seller
            var order = await _context.Order
                .Include(o => o.Customer)
                .Include(o => o.Seller)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.OrderId == orderId && o.SellerUserId == seller.UserId);
            if (order == null)
            {
                TempData["OrderError"] = "Order not found.";
                return RedirectToAction("Home", new { tab = "Order" });
            }

            // Role check: seller is not allowed to set CANCELED or RETURN_REFUND.
            // This is enforced server-side regardless of what the UI shows.
            // if (newStatus == OrderStatus.CANCELED || newStatus == OrderStatus.RETURN_REFUND)
            // {
            //     TempData["OrderError"] = $"Sellers cannot set an order to {newStatus}. " +
            //                               "This action can only be performed by the customer.";
            //     return RedirectToAction("Home", new { tab = "Order" });
            // }

            // Check against the seller-specific transition map
            var sellerAllowed = Order.SellerAllowedTransitions.TryGetValue(
                order.CurrentStatus, out var sellerNext) && sellerNext.Contains(newStatus);

            if (!sellerAllowed)
            {
                // TempData["OrderError"] =
                //     $"Cannot update order #{orderId} from {order.CurrentStatus} to {newStatus}.";
                return RedirectToAction("Home", new { tab = "Order" });
            }

            // Attach observers — they are called inside SetStatusAsync()
            order.Attach(new CustomerNotificationObserver(_notificationService));
            order.Attach(new SellerNotificationObserver(_notificationService));
            order.Attach(new AdminNotificationObserver(_notificationService, _context));
            order.Attach(new CustomerServiceNotificationObserver(_notificationService, _context));

            // SetStatusAsync does the final validation, stamps timestamps, notifies observers
            await order.SetStatusAsync(newStatus);

            await _context.SaveChangesAsync();

            TempData["OrderSuccess"] = $"Order #{orderId} has been updated to {newStatus}.";
            return RedirectToAction("Home", new { tab = "Order" });
        }

        public IActionResult Chat()
        {
            return RedirectToAction("SellerInbox", "Chat");
        }

        [HttpGet]
        public async Task<IActionResult> GetCancelRequest(int orderId)
        {
            var seller = await GetCurrentSellerAsync();
            if (seller == null) return Json(new { success = false, message = "Unauthorized" });

            var order = await _context.Order
                .Include(o => o.Customer)
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.SellerUserId == seller.UserId);

            if (order == null)
                return Json(new { success = false, message = "Order not found" });

            return Json(new
            {
                success = true,
                orderId = order.OrderId,
                customerName = order.Customer?.FullName ?? "Customer",
                cancelReason = order.CancelReason ?? "No reason provided",
                canceledAt = order.CanceledAt?.ToString("dd MMM yyyy, hh:mm tt") ?? "—"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveCancel(int orderId)
        {
            var seller = await GetCurrentSellerAsync();
            if (seller == null) return Json(new { success = false, message = "Unauthorized" });

            var order = await _context.Order
                .Include(o => o.Customer)
                .Include(o => o.Seller)
                .FirstOrDefaultAsync(o => o.OrderId == orderId
                                    && o.SellerUserId == seller.UserId
                                    && o.CurrentStatus == OrderStatus.CANCEL_REQUESTED);

            if (order == null)
                return Json(new { success = false, message = "Order not found." });

            order.CurrentStatus = OrderStatus.CANCELED;
            order.CanceledAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            try
            {
                if (order.Customer?.UserId != null)
                {
                    await _notificationService.CreateAsync(
                        userId: order.Customer.UserId,
                        title: "Cancellation Approved",
                        message: $"Your cancellation request for Order #{orderId} from {order.Seller?.ShopName ?? "the seller"} has been approved."
                    );
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SellerController] Cancel approval notification failed for order #{orderId}: {ex.Message}");
            }

            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectCancel(int orderId)
        {
            var seller = await GetCurrentSellerAsync();
            if (seller == null) return Json(new { success = false, message = "Unauthorized" });

            var order = await _context.Order
                .Include(o => o.Customer)
                .Include(o => o.Seller)
                .FirstOrDefaultAsync(o => o.OrderId == orderId
                                    && o.SellerUserId == seller.UserId
                                    && o.CurrentStatus == OrderStatus.CANCEL_REQUESTED);

            if (order == null)
                return Json(new { success = false, message = "Order not found." });

            order.CurrentStatus = OrderStatus.PREPARING;
            order.CancelReason = null;

            await _context.SaveChangesAsync();

            try
            {
                if (order.Customer?.UserId != null)
                {
                    await _notificationService.CreateAsync(
                        userId: order.Customer.UserId,
                        title: "Cancellation Rejected",
                        message: $"Your cancellation request for Order #{orderId} from {order.Seller?.ShopName ?? "the seller"} has been rejected. Your order is still being prepared."
                    );
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SellerController] RejectCancel notification failed for order #{orderId}: {ex.Message}");
            }

            return Json(new { success = true });
        }
    }
    
    
}