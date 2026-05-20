using System.Security.Claims;
using EcommerceSystem.DTOs;
using EcommerceSystem.Interfaces;
using EcommerceSystem.Models;
using EcommerceSystem.Models.ViewModels;
using EcommerceSystem.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceSystem.Controllers
{
    [Authorize(Roles = "Customer")]
    public class CustomerController : Controller
    {
        private readonly ICustomerContext _customerContext;
        private readonly IProductService _productService;
        private readonly ICartService _cartService;
        private readonly IOrderService _orderService;
        private readonly IReviewService _reviewService;
        private readonly IProfileService _profileService;
        private readonly INotificationService _notificationService;

        public CustomerController(
            ICustomerContext customerContext,
            IProductService productService,
            ICartService cartService,
            IOrderService orderService,
            IReviewService reviewService,
            IProfileService profileService,
            INotificationService notificationService)
        {
            _customerContext = customerContext;
            _productService = productService;
            _cartService = cartService;
            _orderService = orderService;
            _reviewService = reviewService;
            _profileService = profileService;
            _notificationService = notificationService;
        }

        // =========================
        // NAVBAR CART COUNT
        // =========================
        private async Task LoadCartCountAsync()
        {
            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            ViewBag.CartCount = await _cartService.GetCartItemCountAsync(customer.UserId);
            ViewBag.NotificationCount = await _notificationService.GetUnreadCountAsync(customer.UserId);
        }

        // =========================
        // HOME PAGE (BROWSING PRODUCTS)
        // =========================
        public async Task<IActionResult> Home(string? search, int? categoryId)
        {
            await LoadCartCountAsync();

            var categories = await _productService.GetCategoriesAsync();
            var products = await _productService.GetBrowseProductsAsync(search, categoryId);

            ViewBag.Category = categories;
            ViewBag.Search = search;
            ViewBag.CategoryId = categoryId;

            return View(products);
        }

        // =========================
        // QUICK ADD DATA (HOME PAGE)
        // =========================
        [HttpGet]
        public async Task<IActionResult> QuickAddData(int id)
        {
            var dto = await _productService.GetQuickAddProductAsync(id);
            return dto == null ? NotFound() : Json(dto);
        }

        // =========================
        // PRODUCT DETAILS
        // =========================
        public async Task<IActionResult> ProductDetails(int id)
        {
            await LoadCartCountAsync();

            var vm = await _productService.GetProductDetailsAsync(id);
            if (vm == null) return NotFound();

            ViewBag.Reviews = vm.Reviews;
            ViewBag.Images = vm.Images;
            ViewBag.Variations = vm.Variations;

            return View(vm.Product);
        }

        // =========================
        // ADD TO CART (PRODUCT DETAILS PAGE)
        // =========================
        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int quantity, string selectedVariations = "{}")
        {
            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            if (customer == null) return Unauthorized();

            var result = await _cartService.AddToCartAsync(customer.UserId, productId, quantity, selectedVariations);

            if (!result.Success)
            {
                TempData["CartError"] = result.Error;
                return RedirectToAction("ProductDetails", new { id = productId });
            }

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
            await LoadCartCountAsync();

            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            if (customer == null) return Unauthorized();

            var checkout = await _cartService.BuildBuyNowCheckoutAsync(customer.UserId, productId, quantity, selectedVariations);

            if (!checkout.Success)
                return RedirectToAction("Home");

            ViewBag.Source = "product";
            ViewBag.ProductId = productId;

            return View("Checkout", checkout.Value);
        }

        // =========================
        // CART PAGE
        // =========================
        public async Task<IActionResult> Cart()
        {
            await LoadCartCountAsync();

            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            if (customer == null) return Unauthorized();

            var cart = await _cartService.GetCartAsync(customer.UserId);
            return View(cart);
        }

        // =========================
        // UPDATE QUANTITY (CART PAGE)
        // =========================
        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(int cartItemId, int quantity)
        {
            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            if (customer == null) return Unauthorized();

            var result = await _cartService.UpdateQuantityAsync(customer.UserId, cartItemId, quantity);
            return result.Success ? Ok() : BadRequest(result.Error);
        }

        // =========================
        // REMOVE ITEM (CART PAGE)
        // =========================
        [HttpPost]
        public async Task<IActionResult> RemoveItem(int cartItemId)
        {
            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            if (customer == null) return Unauthorized();

            var result = await _cartService.RemoveItemAsync(customer.UserId, cartItemId);
            return result.Success ? Ok() : BadRequest(result.Error);
        }

        // =========================
        // CHECKOUT (CART PAGE)
        // =========================
        public async Task<IActionResult> Checkout(string? selectedItems, string? source, int? productId)
        {
            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            if (customer == null) return Unauthorized();

            ViewBag.Source = source;
            ViewBag.ProductId = productId;

            if (string.IsNullOrWhiteSpace(selectedItems))
                return RedirectToAction("Cart");

            var ids = selectedItems
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse)
                .ToList();

            var result = await _cartService.BuildCartCheckoutAsync(customer.UserId, ids);

            if (!result.Success)
                return RedirectToAction("Cart");

            ViewBag.Source = "cart";
            ViewBag.ProductId = null;

            return View("Checkout", result.Value);
        }

        // =========================
        // PLACE ORDER
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
            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            if (customer == null) return Unauthorized();

            var req = new PlaceOrderRequest
            {
                CustomerId = customer.UserId,
                SelectedAddressId = selectedAddressId,
                PaymentMethod = paymentMethod,
                SelectedItemIds = selectedItemIds ?? new List<int>(),
                Source = source ?? "cart",
                ProductId = productId,
                BuyNowQuantity = buyNowQuantity,
                BuyNowSelectedVariations = buyNowSelectedVariations ?? "{}",
                SellerMessages = ExtractSellerMessagesFromForm(Request.Form)
            };

            var result = await _orderService.PlaceOrderAsync(req);

            if (!result.Success)
            {
                TempData["OrderError"] = result.Error;
                return RedirectToAction(source == "product" ? "ProductDetails" : "Cart", new { id = productId });
            }

            return RedirectToAction("PurchaseHistory");
        }

        private Dictionary<string, string> ExtractSellerMessagesFromForm(IFormCollection form)
        {
            // Keeps controller responsibility: HTTP form parsing
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var key in form.Keys)
            {
                if (key.StartsWith("SellerMessage_", StringComparison.OrdinalIgnoreCase))
                {
                    dict[key] = form[key].ToString();
                }
            }

            return dict;
        }

        // =========================
        // PURCHASE HISTORY
        // =========================
        public async Task<IActionResult> PurchaseHistory()
        {
            await LoadCartCountAsync();

            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            if (customer == null) return Unauthorized();

            var orders = await _orderService.GetPurchaseHistoryAsync(customer.UserId);
            return View(orders);
        }

        // =========================
        // CANCEL ORDER (PURCHASE HISTORY PAGE)
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(int orderId, string cancelReason)
        {
            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            if (customer == null) return Unauthorized();

            var result = await _orderService.CancelOrderAsync(customer.UserId, orderId, cancelReason);
            return Json(new { success = result.Success, message = result.Error });
        }

        // =========================
        // CONFIRM RECEIVED (PURCHASE HISTORY PAGE)
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmReceived(int orderId)
        {
            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            if (customer == null) return Unauthorized();

            var result = await _orderService.ConfirmReceivedAsync(customer.UserId, orderId);
            return Json(new { success = result.Success, message = result.Error });
        }

        // =========================
        // SUBMIT RATING (PURCHASE HISTORY PAGE)
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitRating(int orderItemId, int rating, string reviewText)
        {
            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            if (customer == null) return Unauthorized();

            var result = await _reviewService.SubmitRatingAsync(customer.UserId, orderItemId, rating, reviewText);
            return Json(new { success = result.Success, message = result.Error });
        }

        // =========================
        // SUBMIT COMPLAINT (PURCHASE HISTORY PAGE)
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitComplaint(int orderId, string complaintText)
        {
            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            if (customer == null) return Unauthorized();

            var result = await _orderService.SubmitComplaintAsync(customer.UserId, orderId, complaintText);
            return Json(new { success = result.Success, message = result.Error });
        }

        // =========================
        // PROFILE
        // =========================
        public async Task<IActionResult> Profile()
        {
            await LoadCartCountAsync();
            
            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            if (customer == null) return Unauthorized();

            var profile = await _profileService.GetProfileAsync(customer.UserId);
            return profile == null ? NotFound() : View(profile);
        }

        // =========================
        // UPDATE PROFILE (PROFILE PAGE)
        // =========================
        [HttpPost]
        public async Task<IActionResult> UpdateProfile(Customer model, IFormFile? profileImage)
        {
            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            if (customer == null) return Unauthorized();

            var result = await _profileService.UpdateProfileAsync(customer.UserId, model, profileImage);

            if (!result.Success)
            {
                TempData["ProfileError"] = result.Error;
                return RedirectToAction("Profile");
            }

            var updated = result.Value!;
            await RefreshSignInAsync(updated);

            TempData["ProfileSuccess"] = "Profile updated successfully.";
            return RedirectToAction("Profile");
        }

        private async Task RefreshSignInAsync(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        }

        // =========================
        // ADD ADDRESS (PROFILE PAGE)
        // =========================
        [HttpPost]
        public async Task<IActionResult> AddAddress(DeliveryField model)
        {
            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            if (customer == null) return Unauthorized();

            var result = await _profileService.AddAddressAsync(customer.UserId, model);
            TempData[result.Success ? "AddressSuccess" : "AddressError"] = result.Success ? "Address added successfully." : result.Error;
            return RedirectToAction("Profile");
        }

        // =========================
        // EDIT ADDRESS (PROFILE PAGE)
        // =========================
        [HttpPost]
        public async Task<IActionResult> EditAddress(DeliveryField model)
        {
            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            if (customer == null) return Unauthorized();

            var result = await _profileService.EditAddressAsync(customer.UserId, model);
            TempData[result.Success ? "AddressSuccess" : "AddressError"] = result.Success ? "Address updated successfully." : result.Error;
            return RedirectToAction("Profile");
        }

        // =========================
        // REMOVE ADDRESS (PROFILE PAGE)
        // =========================
        [HttpPost]
        public async Task<IActionResult> RemoveAddress(int addressId)
        {
            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            if (customer == null) return Unauthorized();

            var result = await _profileService.RemoveAddressAsync(customer.UserId, addressId);
            TempData[result.Success ? "AddressSuccess" : "AddressError"] = result.Success ? "Address removed successfully." : result.Error;
            return RedirectToAction("Profile");
        }

        // =========================
        // CHANGE PASSWORD (PROFILE PAGE)
        // =========================
        [HttpGet]
        public async Task<IActionResult> ChangePassword()
        {
            await LoadCartCountAsync();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            if (customer == null) return Unauthorized();

            var result = await _profileService.ChangePasswordAsync(customer.UserId, model.OldPassword, model.NewPassword);

            if (!result.Success)
            {
                ModelState.AddModelError("OldPassword", result.Error ?? "Failed to change password.");
                return View(model);
            }

            TempData["PasswordSuccess"] = "Password changed successfully.";
            return RedirectToAction("Profile");
        }

        // =========================
        // CHAT
        // =========================
        public async Task<IActionResult> Chat() 
        {
            await LoadCartCountAsync();
            return View();
        }

        // =========================
        // NOTIFICATIONS
        // =========================
        public async Task<IActionResult> Notifications()
        {
            await LoadCartCountAsync();

            var customer = await _customerContext.GetCurrentCustomerAsync(User);

            if (customer == null)
                return Unauthorized();

            var notifications =
                await _notificationService.GetUserNotificationsAsync(customer.UserId);

            return View(notifications);
        }
    }
}