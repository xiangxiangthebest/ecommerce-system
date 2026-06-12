using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using EcommerceSystem.Data;
using System.Security.Claims;
using EcommerceSystem.Models;
using EcommerceSystem.Interfaces;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EcommerceSystem.Controllers
{
    [Authorize(Roles = "CustomerService")]
    public class CustomerServiceController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ICustomerServiceContext _customerServiceContext;
        private readonly INotificationService _notificationService;

        public CustomerServiceController(
            AppDbContext context,
            INotificationService notificationService,
            ICustomerServiceContext customerServiceContext)
        {
            _context = context;
            _notificationService = notificationService;
            _customerServiceContext = customerServiceContext;
        }

        private int GetCurrentUserId()
            => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

        private async Task LoadNavbarAsync()
            {
                var customer = await _customerServiceContext.GetCurrentCustomerServiceAsync(User);
                if (customer == null) return;

                var notifications = await _notificationService.GetForUserAsync(customer.UserId);
                ViewBag.UnreadNotificationCount = notifications?.Count(n => !n.IsRead) ?? 0;
            }

        public async Task<IActionResult> Home()
        {
            await LoadNavbarAsync();

            var requests = await _context.Request
                .Include(r => r.RequestUser)
                .ToListAsync();

            ViewBag.RequestIssueType = Enum.GetValues(typeof(EcommerceSystem.Enums.RequestIssueType))
            .Cast<EcommerceSystem.Enums.RequestIssueType>()
            .Select(e => new SelectListItem
            {
                Value = e.ToString(),
                Text = e.ToString()
            }).ToList();

            return View(requests);
        }

        public async Task<IActionResult> Profile()
        {
            await LoadNavbarAsync();
            var cs = await _customerServiceContext.GetCurrentCustomerServiceAsync(User);
            if (cs == null) return NotFound();
            return View(cs);
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(EcommerceSystem.Models.CustomerService model)
        {
            var cs = await _customerServiceContext.GetCurrentCustomerServiceAsync(User);

            if (cs == null) return NotFound();
            
            cs.FullName = model.FullName;
            cs.PhoneNumber = model.PhoneNumber;

            await _context.SaveChangesAsync();

            TempData["ProfileSuccess"] = "Profile updated successfully.";
            return RedirectToAction("Profile");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveReturn(int orderId,
            List<int> approveItemIds, List<int> approveQtys, string? returnType)
        {
            var order = await _context.Order
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderId == orderId
                    && (o.CurrentStatus == OrderStatus.RETURN_REFUND
                        || o.CurrentStatus == OrderStatus.AFTER_SALES_REQUESTED));

            if (order == null)
                return Json(new { success = false, message = "Request not found or no longer pending." });

            if (order.ReturnApprovedAt.HasValue)
                return Json(new { success = false, message = "This request has already been approved." });

            if (approveItemIds == null || approveItemIds.Count == 0)
                return Json(new { success = false, message = "Please select at least one item to approve." });

            // Authoritative return type comes from the AfterSalesRequest row.
            var request = await _context.Request
                .FirstOrDefaultAsync(r => r.OrderId == orderId);

            bool isReturnRefund = request != null
                ? request.RequestServiceType == EcommerceSystem.Enums.RequestServiceType.RETURN_REFUND
                : string.Equals(returnType, "ReturnRefund", StringComparison.OrdinalIgnoreCase);

            var orderItemMap = order.OrderItems.ToDictionary(oi => oi.OrderItemId);
            var pairs = approveItemIds.Zip(approveQtys, (id, qty) => (id, qty)).ToList();

            // Per-item discounted price (TotalAmount = post-voucher merchandise total).
            decimal merchandiseSubtotal = order.OrderItems.Sum(oi => oi.Price * oi.Quantity);
            decimal voucherDiscount = Math.Max(0m, merchandiseSubtotal - order.TotalAmount);
            decimal discountRatio = merchandiseSubtotal > 0
                ? voucherDiscount / merchandiseSubtotal
                : 0m;

            decimal approvedRefundAmount = 0;
            foreach (var (itemId, qty) in pairs)
            {
                if (!orderItemMap.TryGetValue(itemId, out var orderItem)) continue;
                if (qty <= 0 || qty > orderItem.Quantity) continue;

                decimal discountedUnitPrice = Math.Round(orderItem.Price * (1 - discountRatio), 2);
                approvedRefundAmount += qty * discountedUnitPrice;

                if (isReturnRefund)
                {
                    var product = await _context.Products.FindAsync(orderItem.ProductId);
                    if (product != null)
                        product.StockQuantity += qty;
                }
            }

            order.ReturnStatus         = EcommerceSystem.Enums.ReturnStatus.Approved;
            order.ReturnApprovedAt     = DateTime.UtcNow;
            order.ApprovedRefundAmount = approvedRefundAmount;
            order.ReturnType           = isReturnRefund
                                        ? EcommerceSystem.Enums.ReturnType.ReturnRefund
                                        : EcommerceSystem.Enums.ReturnType.RefundOnly;

            // Move to RETURN_REFUND so the seller revenue calc picks it up.
            order.CurrentStatus = OrderStatus.RETURN_REFUND;

            // Stamp the Request row so ApprovedAt is no longer NULL.
            if (request != null)
            {
                request.ApprovedAt  = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            // ── Notify Customer, Seller, and Admin ───────────────────────────
            try
            {
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
                    var total           = approvedRefundAmount;

                    if (customerId.HasValue)
                    {
                        await _notificationService.CreateAsync(
                            userId:  customerId.Value,
                            title:   "Return & Refund Approved",
                            message: $"Your {returnTypeLabel} request for Order #{orderId} from {shopName} " +
                                    $"has been approved by Customer Service. Total: RM{total:F2}"
                        );
                    }

                    if (sellerId.HasValue)
                    {
                        await _notificationService.CreateAsync(
                            userId:  sellerId.Value,
                            title:   "Return & Refund Approved",
                            message: $"Customer Service approved a {returnTypeLabel} request for Order #{orderId} " +
                                    $"from {customerName}. Refund total: RM{total:F2}"
                        );
                    }

                    var adminIds = await _context.Users
                        .Where(u => u.Role == "Admin" && u.IsActive)
                        .Select(u => u.UserId)
                        .ToListAsync();

                    foreach (var adminId in adminIds)
                    {
                        await _notificationService.CreateAsync(
                            userId:  adminId,
                            title:   "Return & Refund Approved",
                            message: $"Order #{orderId} — Customer Service approved a {returnTypeLabel} " +
                                    $"request from {customerName} ({shopName}). Total: RM{total:F2}"
                        );
                    }

                    // Notify all CustomerService users (confirmation that the case was resolved)
                    var csUserIds = await _context.Users
                        .Where(u => u.Role == "CustomerService" && u.IsActive)
                        .Select(u => u.UserId)
                        .ToListAsync();

                    foreach (var csUserId in csUserIds)
                    {
                        await _notificationService.CreateAsync(
                            userId:  csUserId,
                            title:   "Return & Refund Approved",
                            message: $"Order #{orderId} — {returnTypeLabel} request from {customerName} " +
                                    $"({shopName}) has been approved. Refund: RM{total:F2}"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[CustomerServiceController] Notification failed for order #{orderId}: {ex.Message}");
            }

            var stockMsg = isReturnRefund
                ? "Stock has been restored."
                : "Stock unchanged (Refund Only — items not physically returned).";

            return Json(new { success = true, message = $"Return approved for Order #{orderId}. {stockMsg}" });
        }
    }
}