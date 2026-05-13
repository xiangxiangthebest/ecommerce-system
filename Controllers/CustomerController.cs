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
                query = query.Where(p =>
                    p.Name.Contains(search) ||
                    p.Description.Contains(search));
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
            ViewBag.Variations = string.IsNullOrEmpty(product.VariationsJson)
                ? new List<object>()
                : JsonSerializer.Deserialize<List<object>>(product.VariationsJson);

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
            TempData["Success"] = "Product added to cart";
            return RedirectToAction("Cart");
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
        // GET CURRENT CUSTOMER
        // =========================
        private async Task<Customer?> GetCurrentCustomerAsync()
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;

            return await _context.Customers
                .FirstOrDefaultAsync(x => x.Email == email);
        }

        // =========================
        // PURCHASE HISTORY
        // =========================
        public IActionResult PurchaseHistory()
        {
            return View();
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
            customer.Address = model.Address;
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

            TempData["Success"] = "Password changed successfully.";

            return RedirectToAction("Profile");
        }
    }
}