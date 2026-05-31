using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using EcommerceSystem.Data;
using System.Security.Claims;
using EcommerceSystem.Models;
using EcommerceSystem.Observers;
using EcommerceSystem.Interfaces;
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

            if (!seller.IsApproved && tab != "General" && tab != "Profile")
                tab = "General";

            ViewBag.ActiveTab  = tab;
            ViewBag.ShopName   = seller.ShopName;
            ViewBag.IsApproved = seller.IsApproved;

            if (tab == "Profile")
            {
                ViewBag.ProfileSeller = seller;
            }

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
                        .Include(o => o.OrderItems)
                            .ThenInclude(oi => oi.Product)
                        .Where(o => o.SellerUserId == seller.UserId)
                        .OrderByDescending(o => o.OrderTime)
                        .ToListAsync();
                    ViewBag.Orders = orders;
                }
            }

            return View();
        }

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
                model.ImagePath      = mergedPaths[0];
                model.ImagePathsJson = JsonSerializer.Serialize(mergedPaths);
            }
            else
            {
                model.ImagePath      = "/images/placeholder.png";
                model.ImagePathsJson = "[]";
            }

            model.VariationsJson = await ProcessVariationImagesAsync(model.VariationsJson, Request.Form.Files);

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
                existing.ImagePath      = mergedPaths[0];
                existing.ImagePathsJson = JsonSerializer.Serialize(mergedPaths);
            }

            existing.VariationsJson = await ProcessVariationImagesAsync(existing.VariationsJson, Request.Form.Files);

            var comboStockSum = SumComboStock(existing.VariationCombosJson);
            if (comboStockSum > 0)
                existing.StockQuantity = comboStockSum;

            TempData["Success"] = existing.IsDraft ? "Product saved as draft." : "Product updated successfully.";
            await _context.SaveChangesAsync();
            return RedirectToAction("Home", new { tab = "Product" });
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
            if (newStatus == OrderStatus.CANCELED || newStatus == OrderStatus.RETURN_REFUND)
            {
                TempData["OrderError"] = $"Sellers cannot set an order to {newStatus}. " +
                                          "This action can only be performed by the customer.";
                return RedirectToAction("Home", new { tab = "Order" });
            }

            // Check against the seller-specific transition map
            var sellerAllowed = Order.SellerAllowedTransitions.TryGetValue(
                order.CurrentStatus, out var sellerNext) && sellerNext.Contains(newStatus);

            if (!sellerAllowed)
            {
                TempData["OrderError"] =
                    $"Cannot update order #{orderId} from {order.CurrentStatus} to {newStatus}.";
                return RedirectToAction("Home", new { tab = "Order" });
            }

            // Attach observers — they are called inside SetStatus()
            order.Attach(new CustomerDashboardObserver(_notificationService));
            order.Attach(new SellerDashboardObserver(_notificationService));
            order.Attach(new AdminPanelObserver(_notificationService, _context));

            // SetStatus does the final validation, stamps timestamps, notifies observers
            order.SetStatus(newStatus);

            await _context.SaveChangesAsync();

            TempData["OrderSuccess"] = $"Order #{orderId} has been updated to {newStatus}.";
            return RedirectToAction("Home", new { tab = "Order" });
        }

        // ─────────────────────────────────────────────────────────────────────
        // APPROVE RETURN / REFUND  (Seller-side)
        //
        // When the seller approves a RETURN_REFUND request the quantities
        // selected by the customer are added back to the product stock.
        // The order stays in RETURN_REFUND status (terminal for both sides);
        // ReturnApprovedAt is stamped so the admin / dashboard can track it.
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveReturn(int orderId,
            List<int> approveItemIds, List<int> approveQtys, string? returnType)
        {
            var seller = await GetCurrentSellerAsync();
            if (seller == null) return RedirectToAction("Login", "Auth");
 
            if (!seller.IsApproved)
            {
                TempData["OrderError"] = "Your account is pending admin approval.";
                return RedirectToAction("Home", new { tab = "General" });
            }
 
            var order = await _context.Order
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderId == orderId
                                     && o.SellerUserId == seller.UserId
                                     && o.CurrentStatus == OrderStatus.RETURN_REFUND);
 
            if (order == null)
            {
                TempData["OrderError"] = "Order not found or not in RETURN_REFUND status.";
                return RedirectToAction("Home", new { tab = "Order" });
            }
 
            if (order.ReturnApprovedAt.HasValue)
            {
                TempData["OrderError"] = "Return has already been approved for this order.";
                return RedirectToAction("Home", new { tab = "Order" });
            }
 
            if (approveItemIds == null || approveItemIds.Count == 0)
            {
                TempData["OrderError"] = "Please select at least one item to approve.";
                return RedirectToAction("Home", new { tab = "Order" });
            }
 
            // ── Stock restoration logic ──────────────────────────────────────
            // ReturnRefund : customer physically sends item back → add stock back.
            // RefundOnly   : item was never received / partially missing → stock
            //                stays as-is (items were never returned to warehouse).
            bool isReturnRefund = string.Equals(returnType, "ReturnRefund",
                                      StringComparison.OrdinalIgnoreCase);
 
            var orderItemMap = order.OrderItems.ToDictionary(oi => oi.OrderItemId);
            var pairs = approveItemIds.Zip(approveQtys, (id, qty) => (id, qty)).ToList();

            // ── Calculate the actual approved refund amount ───────────────────
            // Sum only approved items × approved qty × unit price.
            // This is what shows in the notification — NOT the full order total.
            decimal approvedRefundAmount = 0;
            foreach (var (itemId, qty) in pairs)
            {
                if (!orderItemMap.TryGetValue(itemId, out var orderItem)) continue;
                if (qty <= 0 || qty > orderItem.Quantity) continue;

                approvedRefundAmount += qty * orderItem.Price;

                if (isReturnRefund)
                {
                    // Physical return: restore the approved quantity to stock
                    var product = await _context.Products.FindAsync(orderItem.ProductId);
                    if (product != null)
                        product.StockQuantity += qty;
                }
                // RefundOnly: no stock change — items were not physically returned
            }

            order.ReturnStatus     = EcommerceSystem.Enums.ReturnStatus.Approved;
            order.ReturnApprovedAt = DateTime.UtcNow;
 
            await _context.SaveChangesAsync();

            // ── Notify Customer, Seller, and Admin after return approval ──────
            // ReturnStatus moves to Approved but CurrentStatus stays RETURN_REFUND,
            // so the standard observers won't fire — we send notifications directly.
            try
            {
                // Reload with nav props so messages can include names/details
                var orderWithDetails = await _context.Order
                    .Include(o => o.Customer)
                    .Include(o => o.Seller)
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Product)
                    .FirstOrDefaultAsync(o => o.OrderId == orderId);

                if (orderWithDetails != null)
                {
                    var returnTypeLabel = isReturnRefund ? "Return & Refund" : "Refund Only";
                    var customerName    = orderWithDetails.Customer?.FullName ?? "Customer";
                    var customerId      = orderWithDetails.Customer?.UserId;
                    var sellerId        = orderWithDetails.Seller?.UserId;
                    var shopName        = orderWithDetails.Seller?.ShopName   ?? "the seller";
                    // Use the calculated approved amount, not the full order total
                    var total           = approvedRefundAmount;

                    // Notify Customer
                    if (customerId.HasValue)
                    {
                        await _notificationService.CreateAsync(
                            userId:  customerId.Value,
                            title:   "Return & Refund Approved",
                            message: $"Your {returnTypeLabel} request for Order #{orderId} from {shopName} " +
                                     $"has been approved. Total: RM{total:F2}"
                        );
                    }

                    // Notify Seller (confirmation echo)
                    if (sellerId.HasValue)
                    {
                        await _notificationService.CreateAsync(
                            userId:  sellerId.Value,
                            title:   "Return & Refund Approved",
                            message: $"You have approved the {returnTypeLabel} request for Order #{orderId} " +
                                     $"from {customerName}. Total: RM{total:F2}"
                        );
                    }

                    // Notify all Admins
                    var adminIds = await _context.Users
                        .Where(u => u.Role == "Admin" && u.IsActive)
                        .Select(u => u.UserId)
                        .ToListAsync();

                    foreach (var adminId in adminIds)
                    {
                        await _notificationService.CreateAsync(
                            userId:  adminId,
                            title:   "Return & Refund Approved",
                            message: $"Order #{orderId} — {shopName} has approved a {returnTypeLabel} " +
                                     $"request from {customerName}. Total: RM{total:F2}"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SellerController] Return approval notification failed for order #{orderId}: {ex.Message}");
            }
 
            var stockMsg = isReturnRefund
                ? "Stock has been restored."
                : "Stock unchanged (Refund Only — items not physically returned).";
 
            TempData["OrderSuccess"] = $"Return approved for Order #{orderId}. {stockMsg}";
            return RedirectToAction("Home", new { tab = "Order" });
        }
    }
}