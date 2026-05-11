using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcommerceSystem.Data;
using EcommerceSystem.Models;
using System.Security.Claims;

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
        // HOME PAGE
        // =========================
        public IActionResult Home()
        {
            ViewBag.Category = _context.Category.ToList();

            var products = _context.Products.ToList();

            return View(products);
        }

        public IActionResult ProductDetails(int id)
        {
            return View();
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
        // ADD TO CART
        // =========================
        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int quantity)
        {
            var customer = await GetCurrentCustomerAsync();

            var cart = await _context.Cart
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.UserId == customer.UserId);

            if (cart == null)
            {
                cart = new Cart
                {
                    UserId = customer.UserId
                };

                _context.Cart.Add(cart);
                await _context.SaveChangesAsync();
            }

            var product = await _context.Products.FindAsync(productId);

            var existingItem = cart.CartItems
                .FirstOrDefault(x => x.ProductId == productId);

            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                cart.CartItems.Add(new CartItem
                {
                    ProductId = productId,
                    Quantity = quantity,
                    Price = product.Price
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Product added to cart";

            return RedirectToAction("Cart");
        }

        // =========================
        // UPDATE CART QUANTITY
        // =========================
        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(int cartItemId, int quantity)
        {
            var item = await _context.CartItem.FindAsync(cartItemId);

            if (item != null)
            {
                item.Quantity = quantity;

                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Cart");
        }

        // =========================
        // REMOVE CART ITEM
        // =========================
        [HttpPost]
        public async Task<IActionResult> RemoveItem(int cartItemId)
        {
            var item = await _context.CartItem.FindAsync(cartItemId);

            if (item != null)
            {
                _context.CartItem.Remove(item);

                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Cart");
        }

        // =========================
        // CHECKOUT PAGE
        // =========================
        [HttpGet]
        public IActionResult Checkout()
        {
            return View();
        }

        // =========================
        // PLACE ORDER
        // =========================
        [HttpPost]
        public async Task<IActionResult> PlaceOrder(
            string paymentMethod,
            string address,
            string sellerMessage)
        {
            // TODO:
            // Create order logic here

            TempData["Success"] = "Order placed successfully";

            return RedirectToAction("PurchaseHistory");
        }

        // =========================
        // PURCHASE HISTORY
        // =========================
        public IActionResult PurchaseHistory()
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
        // CHAT PAGE
        // =========================
        public async Task<IActionResult> Chat(int sellerId)
        {
            var messages = await _context.ChatMessage
                .Where(m => m.SenderId == sellerId ||
                            m.ReceiverId == sellerId)
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            return View(messages);
        }

        // =========================
        // SEND MESSAGE
        // =========================
        [HttpPost]
        public async Task<IActionResult> SendMessage(ChatMessage model)
        {
            _context.ChatMessage.Add(model);

            await _context.SaveChangesAsync();

            return RedirectToAction("Chat", new
            {
                sellerId = model.ReceiverId
            });
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

            return RedirectToAction("Profile");
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

        private async Task LoadCurrentUserToViewBag()
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;

            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Email == email);

            if (user != null)
            {
                ViewBag.FullName = user.FullName;
            }
        }
    }
}