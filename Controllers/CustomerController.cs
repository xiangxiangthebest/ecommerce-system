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
        // ADD TO CART
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
        public async Task<IActionResult> Checkout(string? selectedItems)
        {
            var customer = await GetCurrentCustomerAsync();

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

            return View(model);
        }

        // =========================
        // PLACE ORDER (CART PAGE)
        // =========================
        [HttpPost]
        public async Task<IActionResult> PlaceOrder(int selectedAddressId, string paymentMethod, List<int> selectedItemIds)
        {
            var customer = await GetCurrentCustomerAsync();

            var cart = await _context.Cart
                .FirstOrDefaultAsync(c => c.UserId == customer.UserId);

            if (cart == null)
            {
                TempData["OrderError"] = "Cart not found.";
                return RedirectToAction("Cart");
            }

            var cartItems = await _context.CartItem
                .Include(c => c.Product)
                .ThenInclude(p => p.Seller)
                .Include(c => c.Cart)
                .Where(c =>
                    selectedItemIds.Contains(c.CartItemId) &&
                    c.Cart.UserId == customer.UserId)
                .ToListAsync();

            if (!cartItems.Any())
            {
                TempData["OrderError"] = "Your cart is empty.";
                return RedirectToAction("Cart");
            }

            decimal totalPayment = cartItems.Sum(c =>
                c.Quantity * (decimal)c.Price
            );

            var address = await _context.DeliveryField
                .FirstOrDefaultAsync(a => a.AddressId == selectedAddressId);

            if (address == null)
            {
                TempData["OrderError"] = "Address not found.";
                return RedirectToAction("Cart");
            }

            var groupedBySeller = cartItems
                .GroupBy(c => new { c.Product.SellerId, c.Product.Seller.ShopName });

            foreach (var group in groupedBySeller)
            {
                var sellerId = group.Key.SellerId;
                var sellerName = group.Key.ShopName;
                var messageKey = $"SellerMessage_{sellerName.Replace(" ", "_")}";
                var customerMessage = Request.Form[messageKey].ToString();
                var orderTotal = group.Sum(c => c.Quantity * (decimal)c.Price);
                
                var order = new Order
                {
                    CustomerUserId = customer.UserId,
                    SellerUserId = sellerId,
                    AddressId = selectedAddressId,
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
                }
            }

            _context.CartItem.RemoveRange(cartItems);

            await _context.SaveChangesAsync();

            TempData["OrderSuccess"] = "Order placed successfully!";

            return RedirectToAction("PurchaseHistory");
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

            return View(orders);
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
        // UPDATE PROFILE
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