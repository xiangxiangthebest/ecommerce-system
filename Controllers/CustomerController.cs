using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcommerceSystem.Data;
using EcommerceSystem.Models;
using System.Security.Claims;
using EcommerceSystem.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using BCrypt.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using EcommerceSystem.DTOs;
using EcommerceSystem.ViewModels;

namespace EcommerceSystem.Controllers
{
    [Authorize(Roles = "Customer")]
    public class CustomerController : Controller
    {
        private readonly AppDbContext _context;

        public CustomerController(AppDbContext context)
        {
            _context = context;
        }

        // =========================
        // HOME PAGE (BROWSING PRODUCTS)
        // =========================
        public IActionResult Home(string? search, int? categoryId)
        {
            ViewBag.Category = _context.Category.ToList();
            ViewBag.Search = search;
            ViewBag.CategoryId = categoryId;

            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Seller)
                .Where(p =>
                    !p.IsDraft &&
                    p.Seller != null &&
                    p.Seller.IsApproved);

            // Search
            if (!string.IsNullOrWhiteSpace(search))
            {
                var keyword = search.Trim();

                query = query.Where(p =>
                    EF.Functions.Like(p.Name, $"%{keyword}%") ||
                    EF.Functions.Like(p.Seller.ShopName, $"%{keyword}%"));
            }

            // Category filter
            if (categoryId.HasValue)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value);
            }

            var products = query.ToList();

            return View(products);
        }

        // =========================
        // QUICK ADD DATA (HOME PAGE)
        // =========================
        [HttpGet]
        public async Task<IActionResult> QuickAddData(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Seller)
                .FirstOrDefaultAsync(p => p.ProductId == id && !p.IsDraft);

            if (product == null)
                return NotFound();

            var images = string.IsNullOrEmpty(product.ImagePathsJson)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(product.ImagePathsJson) ?? new List<string>();

            // Fall back to single ImagePath if no list
            if (images.Count == 0 && !string.IsNullOrEmpty(product.ImagePath))
                images.Add(product.ImagePath);

            var variations = string.IsNullOrEmpty(product.VariationsJson)
                ? new List<VariationGroupDto>()
                : JsonSerializer.Deserialize<List<VariationGroupDto>>(
                    product.VariationsJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new List<VariationGroupDto>();

            List<Product.VariationCombo> combos;
            try
            {
                combos = JsonSerializer.Deserialize<List<Product.VariationCombo>>(
                            product.VariationCombosJson ?? "[]")
                        ?? new List<Product.VariationCombo>();
            }
            catch
            {
                combos = new List<Product.VariationCombo>();
            }

            return Json(new
            {
                productId    = product.ProductId,
                name         = product.Name,
                price        = product.Price,
                sku          = product.SKU,
                description  = product.Description,
                stockQuantity = product.StockQuantity,
                images,
                variations,
                variationCombosJson = product.VariationCombosJson ?? "[]",
                shopName     = product.Seller?.ShopName ?? "Unknown Shop"
            });
        }

        // =========================
        // PRODUCT DETAILS
        // =========================
        public IActionResult ProductDetails(int id)
        {
            var product = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Seller)
                .FirstOrDefault(p =>
                    p.ProductId == id &&
                    !p.IsDraft);

            if (product == null)
                return NotFound();

            var reviews = _context.Reviews
                .Where(r => r.ProductId == id)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            ViewBag.Reviews = reviews;

            // Deserialize image list
            ViewBag.Images = string.IsNullOrEmpty(product.ImagePathsJson)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(product.ImagePathsJson);

            // Deserialize variations
            var variations = string.IsNullOrEmpty(product.VariationsJson)
                ? new List<VariationGroupDto>()
                : JsonSerializer.Deserialize<List<VariationGroupDto>>(
                    product.VariationsJson,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            ViewBag.Variations = variations;

            return View(product);
        }

        // =========================
        // ADD TO CART (PRODUCT DETAILS PAGE)
        // =========================
        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int quantity, string selectedVariations = "{}")
        {
            var customer = await GetCurrentCustomerAsync();

            var cart = await _context.Cart
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.UserId == customer.UserId);

            if (cart == null)
            {
                cart = new Cart { UserId = customer.UserId };
                _context.Cart.Add(cart);
                await _context.SaveChangesAsync();
            }

            var product = await _context.Products.FindAsync(productId);

            if (product == null)
            {
                return NotFound();
            }

            // Match existing item by product AND same variation combo
            var existingItem = cart.CartItems
                .FirstOrDefault(x => x.ProductId == productId
                                && x.SelectedVariations == selectedVariations);

            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                cart.CartItems.Add(new CartItem
                {
                    ProductId          = productId,
                    Quantity           = quantity,
                    Price              = product.Price,
                    SelectedVariations = selectedVariations
                });
            }

            await _context.SaveChangesAsync();

            TempData["CartSuccess"] = "Product added to cart";

            return RedirectToAction("ProductDetails", new { id = productId });
        }

        // =========================
        // BUY NOW (PRODUCT DETAILS PAGE)
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BuyNow(int productId, int quantity, string selectedVariations)
        {
            var customer = await GetCurrentCustomerAsync();

            var product = await _context.Products
                .Include(p => p.Seller)
                .FirstOrDefaultAsync(p => p.ProductId == productId);

            if (product == null)
                return RedirectToAction("Home");

            // Create temporary checkout item
            var buyNowItem = new CartItem
            {
                CartItemId = 0,
                ProductId = product.ProductId,
                Product = product,
                Quantity = quantity,
                Price = product.Price,
                SelectedVariations = selectedVariations ?? "{}"
            };

            var addresses = await _context.DeliveryField
                .Where(a => a.UserId == customer.UserId)
                .ToListAsync();

            var model = new Checkout
            {
                Customer = customer,
                CartItems = new List<CartItem> { buyNowItem },
                Addresses = addresses
            };

            ViewBag.Source = "product";
            ViewBag.ProductId = productId;

            return View("Checkout", model);
        }

        // =========================
        // CART PAGE
        // =========================
        public async Task<IActionResult> Cart()
        {
            var customer = await GetCurrentCustomerAsync();

            var cart = await _context.Cart
                .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Product)
                .ThenInclude(p => p.Seller)
                .FirstOrDefaultAsync(c => c.UserId == customer.UserId);

            if (cart == null)
            {
                cart = new Cart
                {
                    CartItems = new List<CartItem>()
                };
            }

            return View(cart);
        }

        // =========================
        // UPDATE QUANTITY (CART PAGE)
        // =========================
        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(int cartItemId, int quantity)
        {
            var customer = await GetCurrentCustomerAsync();

            var item = await _context.CartItem
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci =>
                    ci.CartItemId == cartItemId &&
                    ci.Cart.UserId == customer.UserId);

            if (item == null)
                return NotFound();

            item.Quantity = quantity;

            await _context.SaveChangesAsync();

            return Ok();
        }

        // =========================
        // REMOVE ITEM (CART PAGE)
        // =========================
        [HttpPost]
        public async Task<IActionResult> RemoveItem(int cartItemId)
        {
            var customer = await GetCurrentCustomerAsync();

            var item = await _context.CartItem
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci =>
                    ci.CartItemId == cartItemId &&
                    ci.Cart.UserId == customer.UserId);

            if (item == null)
                return NotFound();

            _context.CartItem.Remove(item);

            await _context.SaveChangesAsync();

            return Ok();
        }

        // =========================
        // CHECKOUT (CART PAGE)
        // =========================
        public async Task<IActionResult> Checkout(string? selectedItems, string? source, int? productId)
        {
            var customer = await GetCurrentCustomerAsync();

            ViewBag.Source = source;
            ViewBag.ProductId = productId;

            if (string.IsNullOrWhiteSpace(selectedItems))
                return RedirectToAction("Cart");

            var addresses = await _context.DeliveryField
                .Where(a => a.UserId == customer.UserId)
                .ToListAsync();

            var ids = selectedItems
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse)
                .ToList();

            var cartItems = await _context.CartItem
                .Include(ci => ci.Product)
                .ThenInclude(p => p.Seller)
                .Include(ci => ci.Cart)
                .Where(ci =>
                    ids.Contains(ci.CartItemId) &&
                    ci.Cart.UserId == customer.UserId)
                .ToListAsync();

            if (!cartItems.Any())
                return RedirectToAction("Cart");

            var model = new Checkout
            {
                Customer = customer,
                CartItems = cartItems,
                Addresses = addresses
            };

            ViewBag.Source = "cart";
            ViewBag.ProductId = null;

            return View("Checkout", model);
        }

        // =========================
        // PLACE ORDER (CART PAGE)
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceOrder(
            int selectedAddressId, 
            string paymentMethod, 
            List<int> selectedItemIds,
            string source,
            int? productId,
            int? buyNowQuantity,
            string? buyNowSelectedVariations)
        {
            var customer = await GetCurrentCustomerAsync();

            var address = await _context.DeliveryField
                .FirstOrDefaultAsync(a => a.AddressId == selectedAddressId && a.UserId == customer.UserId);

            var cart = await _context.Cart
                .FirstOrDefaultAsync(c => c.UserId == customer.UserId);

            if (cart == null)
            {
                TempData["OrderError"] = "Cart not found.";
                return RedirectToAction("Cart");
            }

            List<CartItem> itemsToPurchase;

            if (source == "product")
            {
                var product = await _context.Products
                    .Include(p => p.Seller)
                    .FirstOrDefaultAsync(p => p.ProductId == productId.Value);

                var qty = buyNowQuantity.Value <= 0 ? 1 : buyNowQuantity.Value;

                itemsToPurchase = new List<CartItem>
                {
                    new CartItem
                    {
                        CartItemId = 0,
                        ProductId = product.ProductId,
                        Product = product,
                        Quantity = qty,
                        Price = product.Price,
                        SelectedVariations = buyNowSelectedVariations ?? "{}"
                    }
                };
            }
            else
            {
                if (selectedItemIds == null || selectedItemIds.Count == 0)
                {
                    TempData["OrderError"] = "No items selected.";
                    return RedirectToAction("Cart");
                }

                itemsToPurchase = await _context.CartItem
                    .Include(ci => ci.Product)
                        .ThenInclude(p => p.Seller)
                    .Include(ci => ci.Cart)
                    .Where(ci =>
                        selectedItemIds.Contains(ci.CartItemId) &&
                        ci.Cart.UserId == customer.UserId)
                    .ToListAsync();

                if (!itemsToPurchase.Any())
                {
                    TempData["OrderError"] = "Your cart is empty.";
                    return RedirectToAction("Cart");
                }
            }

            var groupedBySeller = itemsToPurchase
                .GroupBy(i => new { i.Product.SellerId, i.Product.Seller.ShopName });

            await using var tx = await _context.Database.BeginTransactionAsync();

            try
            {
                foreach (var group in groupedBySeller)
                {
                    var sellerId = group.Key.SellerId;
                    var sellerName = group.Key.ShopName;
                    var messageKey = $"SellerMessage_{sellerName.Replace(" ", "_")}";
                    var customerMessage = Request.Form[messageKey].ToString();
                    var orderTotal = group.Sum(i => i.Quantity * (decimal)i.Price);

                    var order = new Order
                    {
                        CustomerUserId = customer.UserId,
                        SellerUserId = sellerId,
                        AddressId = address.AddressId,

                        DeliveryRecipientName = address.RecipientName,
                        DeliveryPhoneNumber = address.PhoneNumber,
                        DeliveryAddressLine1 = address.AddressLine1,
                        DeliveryAddressLine2 = address.AddressLine2,
                        DeliveryCity = address.City,
                        DeliveryPostcode = address.Postcode,
                        DeliveryState = address.State,

                        TotalAmount = orderTotal,
                        PaymentMethod = paymentMethod,
                        CurrentStatus = OrderStatus.PENDING,
                        OrderTime = DateTime.Now,
                        CustomerMessage = customerMessage
                    };

                    _context.Order.Add(order);
                    await _context.SaveChangesAsync();

                    foreach (var item in group)
                    {
                        var orderItem = new OrderItem
                        {
                            OrderId = order.OrderId,
                            ProductId = item.ProductId,
                            Quantity = item.Quantity,
                            Price = (decimal)item.Price,
                            SelectedVariation = item.SelectedVariations
                        };

                        _context.OrderItems.Add(orderItem);

                        item.Product.StockQuantity -= item.Quantity;

                        if (source != "product" && item.CartItemId != 0)
                        {
                            _context.CartItem.Remove(item);
                        }
                    }

                    await _context.SaveChangesAsync();
                }

                await tx.CommitAsync();

                return RedirectToAction("PurchaseHistory");
            }
            catch
            {
                await tx.RollbackAsync();
                TempData["OrderError"] = "Failed to place order. Please try again.";
                return RedirectToAction(source == "product" ? "ProductDetails" : "Cart", new { id = productId });
            }
        }

        // =========================
        // PURCHASE HISTORY
        // =========================
        public async Task<IActionResult> PurchaseHistory()
        {
            var customer = await GetCurrentCustomerAsync();

            var orders = await _context.Order
                .Where(o => o.CustomerUserId == customer.UserId)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Seller)
                .Include(o => o.Address)
                .OrderByDescending(o => o.OrderTime)
                .ToListAsync();

            var orderItemIds = orders
                    .SelectMany(o => o.OrderItems)
                    .Select(oi => oi.OrderItemId)
                    .ToList();

                if (orderItemIds.Count > 0)
                {
                    var reviewedItemIds = await _context.Reviews
                        .Where(r => r.CustomerId == customer.UserId && orderItemIds.Contains(r.OrderItemId))
                        .Select(r => r.OrderItemId)
                        .ToListAsync();

                    var reviewedSet = reviewedItemIds.ToHashSet();

                    foreach (var o in orders)
                    {
                        o.ReviewSubmitted = o.OrderItems.Count > 0
                            && o.OrderItems.All(oi => reviewedSet.Contains(oi.OrderItemId));
                    }
                }

            return View(orders);
        }

        // =========================
        // CANCEL ORDER (POST)
        // Only allowed from PENDING
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(int orderId, string cancelReason)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return Unauthorized();

            var order = await _context.Order
                .FirstOrDefaultAsync(o => o.OrderId == orderId
                                    && o.CustomerUserId == customer.UserId
                                    && o.CurrentStatus == OrderStatus.PENDING);

            if (order == null)
                return Json(new { success = false, message = "Order cannot be canceled." });

            if (string.IsNullOrWhiteSpace(cancelReason))
                return Json(new { success = false, message = "Please provide a cancellation reason." });

            order.CurrentStatus   = OrderStatus.CANCELED;
            order.CancelReason    = cancelReason.Trim();
            order.CanceledAt      = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // =========================
        // CONFIRM RECEIVED (POST)
        // Moves DELIVERED → RECEIVED
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmReceived(int orderId)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return Unauthorized();

            var order = await _context.Order
                .FirstOrDefaultAsync(o => o.OrderId == orderId
                                    && o.CustomerUserId == customer.UserId
                                    && o.CurrentStatus == OrderStatus.DELIVERED);

            if (order == null)
                return Json(new { success = false, message = "Order not found or already confirmed." });

            order.CurrentStatus = OrderStatus.RECEIVED;
            order.ReceivedAt    = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // =========================
        // SUBMIT RATING (POST)
        // Only for RECEIVED orders
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitRating(int orderItemId, int rating, string reviewText)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return Unauthorized();

            // Verify order belongs to customer and is RECEIVED
            var orderItem = await _context.OrderItems
                .Include(oi => oi.Order)
                .FirstOrDefaultAsync(oi => oi.OrderItemId == orderItemId
                                        && oi.Order.CustomerUserId == customer.UserId
                                        && oi.Order.CurrentStatus  == OrderStatus.RECEIVED);

            if (orderItem == null)
                return Json(new { success = false, message = "Cannot review this item." });

            // Check duplicate
            var existing = await _context.Reviews
                .FirstOrDefaultAsync(r => r.OrderItemId == orderItem.OrderItemId
                                    && r.CustomerId  == customer.UserId);
            if (existing != null)
                return Json(new { success = false, message = "You have already reviewed this item." });

            if (rating < 1 || rating > 5)
                return Json(new { success = false, message = "Rating must be 1–5." });

            var review = new Review
            {
                OrderItemId  = orderItem.OrderItemId,
                ProductId    = orderItem.ProductId,
                CustomerId   = customer.UserId,
                Rating       = rating,
                ReviewText   = reviewText?.Trim() ?? "",
                CreatedAt    = DateTime.UtcNow
            };

            _context.Reviews.Add(review);

            // Recalculate product average
            var product = await _context.Products.FindAsync(orderItem.ProductId);
            if (product != null)
            {
                var allRatings = await _context.Reviews
                    .Where(r => r.ProductId == orderItem.ProductId)
                    .Select(r => r.Rating)
                    .ToListAsync();
                allRatings.Add(rating);
                product.AverageRating = allRatings.Average();
                product.ReviewCount   = allRatings.Count;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // =========================
        // SUBMIT COMPLAINT (POST)
        // For RECEIVED or RETURN_REFUND orders
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitComplaint(int orderId, string complaintText)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return Unauthorized();

            var order = await _context.Order
                .FirstOrDefaultAsync(o => o.OrderId == orderId
                                    && o.CustomerUserId == customer.UserId
                                    && (o.CurrentStatus == OrderStatus.RECEIVED
                                        || o.CurrentStatus == OrderStatus.RETURN_REFUND));

            if (order == null)
                return Json(new { success = false, message = "Order not eligible for complaint." });

            if (string.IsNullOrWhiteSpace(complaintText))
                return Json(new { success = false, message = "Please describe your complaint." });

            order.ComplaintText      = complaintText.Trim();
            order.ComplaintSubmitted = true;
            order.ComplaintAt        = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // =========================
        // GET CURRENT CUSTOMER
        // =========================
        private async Task<Customer?> GetCurrentCustomerAsync()
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;

            return await _context.Customers
                .FirstOrDefaultAsync(x => x.Email == email);
        }
        
        // =========================
        // CHAT
        // =========================
        public IActionResult Chat()
        {
            return View();
        }

        // =========================
        // NOTIFICATIONS
        // =========================
        public IActionResult Notifications()
        {
            return View();
        }

        // =========================
        // PROFILE PAGE
        // =========================
        public async Task<IActionResult> Profile()
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;

            var customer = await _context.Users
                .OfType<Customer>()
                .Include(x => x.Addresses)
                .FirstOrDefaultAsync(x => x.Email == email);

            if (customer == null)
                return NotFound();

            return View(customer);
        }

        // =========================
        // UPDATE PROFILE (PROFILE PAGE)
        // =========================
        [HttpPost]
        public async Task<IActionResult> UpdateProfile(Customer model, IFormFile profileImage)
        {
            var customer = await _context.Users
                .OfType<Customer>()
                .FirstOrDefaultAsync(x => x.UserId == model.UserId);

            if (customer == null)
                return NotFound();

            customer.FullName = model.FullName;
            customer.Email = model.Email;
            customer.Phone = model.Phone;
            customer.Gender = model.Gender;
            customer.Birthday = model.Birthday;

            if (profileImage != null && profileImage.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/profile");

                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                var fileName = Guid.NewGuid() + Path.GetExtension(profileImage.FileName);
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await profileImage.CopyToAsync(stream);
                }

                customer.ProfilePicture = "/images/profile/" + fileName;
            }

            await _context.SaveChangesAsync();

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, customer.FullName),
                new Claim(ClaimTypes.Email, customer.Email),
                new Claim(ClaimTypes.Role, customer.Role)
            };

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal);

            TempData["ProfileSuccess"] = "Profile updated successfully.";

            return RedirectToAction("Profile");
        }

        // =========================
        // DELIVERY FIELDS (PROFILE PAGE)
        // =========================
        [HttpPost]
        public async Task<IActionResult> AddAddress(DeliveryField model)
        {
            var customer = await _context.Users
                .OfType<Customer>()
                .Include(x => x.Addresses)
                .FirstOrDefaultAsync(x => x.UserId == model.UserId);

            if (customer == null)
                return NotFound();

            // Maximum 3 addresses
            if (customer.Addresses.Count >= 3)
            {
                TempData["AddressError"] = "Maximum 3 addresses allowed.";
                return RedirectToAction("Profile");
            }

            // First address automatically default
            if (!customer.Addresses.Any())
            {
                model.IsDefault = true;
            }
            else if (model.IsDefault)
            {
                foreach (var addr in customer.Addresses)
                {
                    addr.IsDefault = false;
                }
            }

            model.UserId = customer.UserId;
            model.Customer = null; // avoid EF confusion

            _context.DeliveryField.Add(model);

            await _context.SaveChangesAsync();

            TempData["AddressSuccess"] = "Address added successfully.";

            return RedirectToAction("Profile");
        }

        // =========================
        // EDIT ADDRESS (PROFILE PAGE)
        // =========================
        [HttpPost]
        public async Task<IActionResult> EditAddress(DeliveryField model)
        {
            var address = await _context.DeliveryField
                .Include(x => x.Customer)
                .ThenInclude(x => x.Addresses)
                .FirstOrDefaultAsync(x => x.AddressId == model.AddressId);

            if (address == null)
                return NotFound();

            address.RecipientName = model.RecipientName;
            address.PhoneNumber = model.PhoneNumber;
            address.AddressLine1 = model.AddressLine1;
            address.AddressLine2 = model.AddressLine2;
            address.City = model.City;
            address.Postcode = model.Postcode;
            address.State = model.State;

            // Set default
            if (model.IsDefault)
            {
                foreach (var addr in address.Customer.Addresses)
                {
                    addr.IsDefault = false;
                }

                address.IsDefault = true;
            }

            await _context.SaveChangesAsync();

            TempData["AddressSuccess"] = "Address updated successfully.";

            return RedirectToAction("Profile");
        }

        // =========================
        // REMOVE ADDRESS (PROFILE PAGE)
        // =========================
        [HttpPost]
        public async Task<IActionResult> RemoveAddress(int addressId)
        {
            var address = await _context.DeliveryField
                .FirstOrDefaultAsync(x => x.AddressId == addressId);

            if (address == null)
                return NotFound();

            int userId = address.UserId;
            bool wasDefault = address.IsDefault;

            _context.DeliveryField.Remove(address);
            await _context.SaveChangesAsync();

            if (wasDefault)
            {
                var next = await _context.DeliveryField
                    .Where(x => x.UserId == userId)
                    .OrderBy(x => x.CreatedAt)
                    .FirstOrDefaultAsync();

                if (next != null)
                {
                    next.IsDefault = true;
                    await _context.SaveChangesAsync();
                }
            }

            return RedirectToAction("Profile");
        }

        // =========================
        // CHANGE PASSWORD PAGE
        // =========================
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        // =========================
        // CHANGE PASSWORD
        // =========================
        [HttpPost]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var email = User.FindFirst(ClaimTypes.Email)?.Value;

            var customer = await _context.Users
                .OfType<Customer>()
                .FirstOrDefaultAsync(x => x.Email == email);

            if (customer == null)
                return NotFound();

            // Verify current password
            bool passwordCorrect = BCrypt.Net.BCrypt.Verify(
                model.OldPassword,
                customer.PasswordHash
            );

            if (!passwordCorrect)
            {
                ModelState.AddModelError(
                    "OldPassword",
                    "Current password is incorrect."
                );

                return View(model);
            }

            // Prevent same password
            if (model.OldPassword == model.NewPassword)
            {
                ModelState.AddModelError(
                    "NewPassword",
                    "New password cannot be same as current password."
                );

                return View(model);
            }

            // Hash new password
            customer.PasswordHash = BCrypt.Net.BCrypt.HashPassword(
                model.NewPassword
            );

            await _context.SaveChangesAsync();

            TempData["PasswordSuccess"] = "Password changed successfully.";

            return RedirectToAction("Profile");
        }
    }
}