using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using EcommerceSystem.Data;
using System.Security.Claims;
using EcommerceSystem.Models;
using EcommerceSystem.Interfaces;

namespace EcommerceSystem.Controllers
{
    // Customer Service only handles two things:
    //   1. Approving the after-sales (Return / Refund) requests ROUTED to it
    //   2. Receiving / reading notifications (the bell)
    //
    // ── ROUTING ──────────────────────────────────────────────────────────────
    // When a customer raises an after-sales request the order goes to
    // OrderStatus.AFTER_SALES_REQUESTED and an AfterSalesRequest row is created
    // in _context.Request. PurchaseHistory.cshtml routes by RequestIssueType:
    //     SELLER  handles : WrongItemReceived, ChangeOfMind, ItemNotAsDescribed
    //     CS      handles : ItemNotDelivered, DamagedDefective,
    //                       MissingPartsAccessories, Other
    //
    // NOTE: change the role string below if your CustomerService accounts
    // use a different Role value in the Users table.
    [Authorize(Roles = "CustomerService")]
    public class CustomerServiceController : Controller
    {
        private readonly AppDbContext _context;
        private readonly INotificationService _notificationService;

        public CustomerServiceController(AppDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        private int GetCurrentUserId()
            => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

        // Issue types Customer Service is responsible for (everything the seller
        // does NOT handle). Keep this in sync with PurchaseHistory.cshtml.
        private static readonly EcommerceSystem.Enums.RequestIssueType[] CsIssueTypes =
        {
            EcommerceSystem.Enums.RequestIssueType.ItemNotDelivered,
            EcommerceSystem.Enums.RequestIssueType.DamagedDefective,
            EcommerceSystem.Enums.RequestIssueType.MissingPartsAccessories,
            EcommerceSystem.Enums.RequestIssueType.Other
        };

        // ─────────────────────────────────────────────────────────────────────
        // HOME — list the after-sales requests routed to Customer Service.
        // Pending ones are AFTER_SALES_REQUESTED; once approved we flip them to
        // RETURN_REFUND (see ApproveReturn) so they still show under "Approved".
        // ─────────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Home()
        {
            var currentUserId = GetCurrentUserId();

            // Notification bell unread count
            var notifications = await _notificationService.GetForUserAsync(currentUserId);
            ViewBag.UnreadNotificationCount = notifications?.Count(n => !n.IsRead) ?? 0;

            // After-sales requests that belong to Customer Service (filter in
            // memory to avoid enum-array translation issues on the DB provider).
            var allRequests = await _context.Request
                .Where(r => r.OrderId != null)
                .ToListAsync();

            var csRequests = allRequests
                .Where(r => CsIssueTypes.Contains(r.RequestIssueType))
                .ToList();

            var csOrderIds = csRequests.Select(r => r.OrderId!.Value).Distinct().ToList();

            var orders = await _context.Order
                .Include(o => o.Customer)
                .Include(o => o.Seller)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Where(o => csOrderIds.Contains(o.OrderId)
                         && (o.CurrentStatus == OrderStatus.AFTER_SALES_REQUESTED   // pending
                          || o.CurrentStatus == OrderStatus.RETURN_REFUND           // approved (flipped)
                          || o.CurrentStatus == OrderStatus.REFUND))
                .OrderByDescending(o => o.ReturnInitiatedAt ?? o.OrderTime)
                .ToListAsync();

            var shownOrderIds = orders.Select(o => o.OrderId).ToHashSet();
            ViewBag.Orders = orders;

            // Per-order display labels built here so the view never has to
            // reference the AfterSalesRequest entity type directly.
            var issueLabel   = new Dictionary<int, string>();
            var serviceLabel = new Dictionary<int, string>();   // "Return & Refund" / "Refund Only"
            var serviceToken = new Dictionary<int, string>();   // "ReturnRefund"    / "RefundOnly"

            foreach (var r in csRequests)
            {
                var oid = r.OrderId!.Value;
                if (!shownOrderIds.Contains(oid) || issueLabel.ContainsKey(oid)) continue;

                issueLabel[oid] = r.RequestIssueType switch
                {
                    EcommerceSystem.Enums.RequestIssueType.WrongItemReceived       => "Wrong item received",
                    EcommerceSystem.Enums.RequestIssueType.ChangeOfMind            => "Change of mind",
                    EcommerceSystem.Enums.RequestIssueType.ItemNotAsDescribed      => "Item not as described",
                    EcommerceSystem.Enums.RequestIssueType.ItemNotDelivered        => "Item not delivered",
                    EcommerceSystem.Enums.RequestIssueType.DamagedDefective        => "Damaged / defective",
                    EcommerceSystem.Enums.RequestIssueType.MissingPartsAccessories => "Missing parts / accessories",
                    _                                                              => "Other"
                };

                bool isRR = r.RequestServiceType == EcommerceSystem.Enums.RequestServiceType.RETURN_REFUND;
                serviceLabel[oid] = isRR ? "Return & Refund" : "Refund Only";
                serviceToken[oid] = isRR ? "ReturnRefund"    : "RefundOnly";
            }

            ViewBag.IssueLabel   = issueLabel;
            ViewBag.ServiceLabel = serviceLabel;
            ViewBag.ServiceToken = serviceToken;

            return View();
        }

        // ─────────────────────────────────────────────────────────────────────
        // APPROVE RETURN / REFUND  (Customer-Service side)
        //
        // Same money + stock logic the seller used to run, but it now works on
        // the AFTER_SALES_REQUESTED status and reads the return type from the
        // AfterSalesRequest row.
        //
        //   * Per-item refund uses the proportional voucher discount
        //     (TotalAmount is the post-voucher merchandise total).
        //   * Return & Refund -> stock is restored (goods came back).
        //   * Refund Only     -> stock stays as-is (goods never returned).
        //   * Sets ReturnStatus / ReturnApprovedAt / ApprovedRefundAmount /
        //     ReturnType, then moves CurrentStatus to RETURN_REFUND.
        //
        // Moving to RETURN_REFUND is what keeps the SELLER's existing revenue
        // calculation working unchanged.
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveReturn(int orderId,
            List<int> approveItemIds, List<int> approveQtys, string? returnType)
        {
            var order = await _context.Order
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderId == orderId
                                     && o.CurrentStatus == OrderStatus.AFTER_SALES_REQUESTED);

            if (order == null)
            {
                TempData["OrderError"] = "Request not found or no longer pending.";
                return RedirectToAction("Home");
            }

            if (order.ReturnApprovedAt.HasValue)
            {
                TempData["OrderError"] = "This request has already been approved.";
                return RedirectToAction("Home");
            }

            if (approveItemIds == null || approveItemIds.Count == 0)
            {
                TempData["OrderError"] = "Please select at least one item to approve.";
                return RedirectToAction("Home");
            }

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
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[CustomerServiceController] Notification failed for order #{orderId}: {ex.Message}");
            }

            var stockMsg = isReturnRefund
                ? "Stock has been restored."
                : "Stock unchanged (Refund Only — items not physically returned).";

            TempData["OrderSuccess"] = $"Return approved for Order #{orderId}. {stockMsg}";
            return RedirectToAction("Home");
        }
    }
}
