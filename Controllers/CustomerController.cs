using System.Security.Claims;
using EcommerceSystem.DTOs;
using EcommerceSystem.Enums;
using EcommerceSystem.Models;
using EcommerceSystem.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EcommerceSystem.Interfaces;
using EcommerceSystem.Data;


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
        private readonly IReturnImageStorage _returnImageStorage;
        private readonly INotificationService _notificationService;
        private readonly IProductReportService _productReportService;
        private readonly IReviewReportService _reviewReportService;
        private readonly AppDbContext _context;


        public CustomerController(
            ICustomerContext customerContext,
            IProductService productService,
            ICartService cartService,
            IOrderService orderService,
            IReviewService reviewService,
            IProfileService profileService,
            IReturnImageStorage returnImageStorage,
            INotificationService notificationService,
            IProductReportService productReportService,
            IReviewReportService reviewReportService,
            AppDbContext context)
        {
            _customerContext = customerContext;
            _productService = productService;
            _cartService = cartService;
            _orderService = orderService;
            _reviewService = reviewService;
            _profileService = profileService;
            _returnImageStorage = returnImageStorage;
            _notificationService = notificationService;
            _productReportService = productReportService;
            _reviewReportService = reviewReportService;
            _context = context;
        }
        
        // =========================
        // NAVBAR HELPERS
        // =========================
        private async Task LoadNavbarAsync()
        {
            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            if (customer == null) return;

            ViewBag.CartCount = await _cartService.GetCartItemCountAsync(customer.UserId);

            var notifications = await _notificationService.GetForUserAsync(customer.UserId);
            ViewBag.UnreadNotificationCount = notifications?.Count(n => !n.IsRead) ?? 0;
        }

        // =========================
        // HOME PAGE (BROWSING PRODUCTS)
        // =========================
        public async Task<IActionResult> Home(string? search, int? categoryId)
        {
            await LoadNavbarAsync();

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
            await LoadNavbarAsync();

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
        public async Task<IActionResult> AddToCart(int productId, int quantity, string selectedVariations = "{}", string? returnUrl = null)
        {
            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            if (customer == null) return Unauthorized();

            var result = await _cartService.AddToCartAsync(customer.UserId, productId, quantity, selectedVariations);

            string? redirectUrl = returnUrl;
            if (string.IsNullOrWhiteSpace(redirectUrl) || !Url.IsLocalUrl(redirectUrl))
            {
                redirectUrl = Url.Action("ProductDetails", "Customer", new { id = productId });
            }

            redirectUrl ??= "/";

            if (!result.Success)
            {
                TempData["CartError"] = result.Error;
                return Redirect(redirectUrl);
            }

            TempData["CartSuccess"] = "Product added to cart";
            return Redirect(redirectUrl);
        }

        // =========================
        // BUY NOW (PRODUCT DETAILS PAGE)
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BuyNow(int productId, int quantity, string selectedVariations)
        {
            await LoadNavbarAsync();

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
            await LoadNavbarAsync();

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
            await LoadNavbarAsync();

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
            await LoadNavbarAsync();

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
        public async Task<IActionResult> CancelOrder(int orderId, string cancelReason, bool request)
        {
            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            if (customer == null) return Unauthorized();

            var orderCheck = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                _context.Order,
                o => o.OrderId == orderId && o.CustomerUserId == customer.UserId
            );

            if (orderCheck == null)
            {
                return Json(new { success = false, message = "Order not found." });
            }

            OperationResult result;

            if (request || orderCheck.CurrentStatus == OrderStatus.PREPARING)
            {
                // 走申请取消流程 -> 状态变为 CANCEL_REQUESTED (状态机完全允许，绝不报错)
                result = await _orderService.RequestCancelOrderAsync(customer.UserId, orderId, cancelReason);
            }
            else
            {
                // 走直接取消流程 -> 状态变为 CANCELED 
                result = await _orderService.CancelOrderAsync(customer.UserId, orderId, cancelReason);
            }

            // 3. 返回安全数据
            return Json(new { success = result.Success, message = result.Error });
    
        }

        // =========================
        // REQUEST RETURN / REFUND (PURCHASE HISTORY PAGE)
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestReturnRefund(int orderId, string reason, List<IFormFile>? images)
        {
            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            if (customer == null) return Unauthorized();

            List<string> imagePaths = new();

            if (images != null && images.Count > 4)
                return Json(new { success = false, message = "Maximum 4 images allowed." });

            if (images != null && images.Any())
                imagePaths = await _returnImageStorage.SaveReturnImagesAsync(images);

            var result = await _orderService.RequestReturnRefundAsync(
                customer.UserId,
                orderId,
                reason,
                imagePaths,
                ReturnInitiatedBy.Customer
            );

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
        public async Task<IActionResult> SubmitRating(int orderItemId, int rating, string reviewText, List<IFormFile> images)
        {
            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            if (customer == null) return Unauthorized();

            var result = await _reviewService.SubmitRatingAsync(customer.UserId, orderItemId, rating, reviewText, images);
            return Json(new { success = result.Success, message = result.Error });
        }

        // =========================
        // PROFILE PAGE
        // =========================
        public async Task<IActionResult> Profile()
        {
            await LoadNavbarAsync();

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
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
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
            await LoadNavbarAsync();
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
        public IActionResult Chat()
        {
            return RedirectToAction("CustomerInbox", "Chat");
        }

        // =========================
        // NOTIFICATIONS
        // =========================
        public async Task<IActionResult> Notifications()
        {
            await LoadNavbarAsync();

            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            if (customer == null) return Unauthorized();
            
            var notifications = await _notificationService.GetForUserAsync(customer.UserId)
                                ?? new List<EcommerceSystem.Models.Notification>();
            return View(notifications);
        }

        // =========================
        // REPORT PRODUCT (PRODUCT DETAILS PAGE)
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReportProduct(int productId, string reportReason, string reportDescription, List<IFormFile> evidenceFiles)
        {
            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            if (customer == null) return Unauthorized();

            try
            {
                var dto = new CreateProductReportDto
                {
                    ProductId = productId,
                    ReportReason = reportReason,
                    ReportDescription = reportDescription
                };

                var report = await _productReportService.CreateProductReportAsync(customer.UserId, dto, evidenceFiles ?? new List<IFormFile>());

                return Json(new { success = true, message = "Report submitted successfully. Thank you for helping us maintain product quality.", reportId = report.ReportId });
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
            catch
            {
                return Json(new { success = false, message = "An error occurred while submitting the report." });
            }
        }

        // =========================
        // GET CUSTOMER'S REPORTS
        // =========================
        [HttpGet]
        public async Task<IActionResult> MyReports()
        {
            await LoadNavbarAsync();

            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            if (customer == null) return Unauthorized();

            var productReports = await _productReportService.GetReportsByCustomerIdAsync(customer.UserId);
            var reviewReports = await _reviewReportService.GetReportsByCustomerIdAsync(customer.UserId);

            var vm = new MyReportsViewModel
            {
                ProductReports = productReports,
                ReviewReports = reviewReports
            };

            return View("Reports", vm);
        }

        // =========================
        // REPORT REVIEW
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReportReview(int reviewId, string reportReason, string reportDescription, List<IFormFile> evidenceFiles)
        {
            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            if (customer == null) return Unauthorized();

            try
            {
                var dto = new CreateReviewReportDto
                {
                    ReviewId = reviewId,
                    ReportReason = reportReason,
                    ReportDescription = reportDescription
                };

                var report = await _reviewReportService.CreateReviewReportAsync(customer.UserId, dto, evidenceFiles ?? new List<IFormFile>());

                return Json(new { success = true, message = "Report submitted successfully. Thank you for helping us maintain review quality.", reportId = report.ReviewReportId });
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                // Log the actual exception for debugging
                string errorMsg = ex.Message;
                if (ex.InnerException != null)
                    errorMsg += " | Inner: " + ex.InnerException.Message;
                
                Console.WriteLine($"Error in ReportReview: {ex.GetType().Name} - {errorMsg}");
                Console.WriteLine($"Stack: {ex.StackTrace}");
                
                return Json(new { success = false, message = errorMsg });
            }
        }

        // =========================
        // GET CUSTOMER'S REVIEW REPORTS
        // =========================
        [HttpGet]
        public async Task<IActionResult> MyReviewReports()
        {
            await LoadNavbarAsync();

            var customer = await _customerContext.GetCurrentCustomerAsync(User);
            if (customer == null) return Unauthorized();

            var reports = await _reviewReportService.GetReportsByCustomerIdAsync(customer.UserId);
            return View("ReviewReports", reports);
        }

    }
}