using Microsoft.AspNetCore.Mvc;
using EcommerceSystem.Enums;
using EcommerceSystem.Interfaces;
using EcommerceSystem.Data;
using EcommerceSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;         


namespace EcommerceSystem.Controllers
{
    public class RequestController : Controller
    {
        private readonly ICustomerContext _customerContext;
        private readonly AppDbContext _context;
        private readonly INotificationService _notificationService;

        public RequestController(
            ICustomerContext customerContext,
            AppDbContext context,
            INotificationService notificationService)
        {
            _customerContext = customerContext;
            _context = context;
            _notificationService = notificationService;
        }

        private async Task<Seller?> GetCurrentSellerAsync()
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            return await _context.Seller.FirstOrDefaultAsync(x => x.Email == email);
        }

        [HttpPost]
        public async Task<IActionResult> CreateAfterSalesRequest(
            int orderId,
            string requestServiceType,
            string requestIssueType,
            string description,
            List<int> requestItemIds,
            List<int> requestItemQtys,
            List<IFormFile>? images)
        {
            var user = await _customerContext.GetCurrentCustomerAsync(User);
            if (user == null) return Json(new { success = false, message = "User not found" });
            var order = await _context.Order.FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null) return Json(new { success = false, message = "Order not found" });

            if (!Enum.TryParse<RequestServiceType>(requestServiceType, out var serviceType))
                return Json(new { success = false, message = "Invalid service type" });

            // RETURN_REFUND and REFUND go straight to customer service → use RETURN_REFUND status
            // so the customer service dashboard (which filters on RETURN_REFUND) picks them up.
            order.CurrentStatus = (serviceType == RequestServiceType.RETURN_REFUND || serviceType == RequestServiceType.REFUND)
                ? OrderStatus.RETURN_REFUND
                : OrderStatus.AFTER_SALES_REQUESTED;

            if (!Enum.TryParse<RequestIssueType>(requestIssueType, true, out var issueType)
                || !Enum.IsDefined(typeof(RequestIssueType), issueType))
                return Json(new { success = false, message = "Invalid issue type" });

            var requestedItems = requestItemIds
                .Zip(requestItemQtys, (id, qty) => new { orderItemId = id, qty })
                .ToList();

            var request = new Request
            {
                RequestUserId = user.UserId,
                OrderId = orderId,
                RequestServiceType = serviceType,  
                RequestIssueType = issueType,
                Description = description,
                RequestedItemsJson = JsonSerializer.Serialize(requestedItems),
            };

            _context.Request.Add(request);
            await _context.SaveChangesAsync();

            if (images != null && images.Count > 0)
            {
                foreach (var file in images)
                {
                    var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
                    var path = Path.Combine("wwwroot/uploads", fileName);

                    using var stream = new FileStream(path, FileMode.Create);
                    await file.CopyToAsync(stream);

                    _context.RequestImage.Add(new RequestImage
                    {
                        RequestId = request.RequestId,
                        ImagePath = fileName
                    });
                }

                await _context.SaveChangesAsync();
            }

            // ── Notify all parties about the new after-sales request ──────────
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
                    var shopName     = orderWithDetails.Seller?.ShopName ?? "the seller";
                    var customerId   = orderWithDetails.Customer?.UserId;
                    var customerName = orderWithDetails.Customer?.FullName ?? "Customer";
                    var sellerId     = orderWithDetails.Seller?.UserId;
                    var total        = orderWithDetails.TotalAmount;
                    var serviceLabel = serviceType == RequestServiceType.RETURN_REFUND
                        ? "Return & Refund" : "Refund Only";

                    // 1. Notify Customer — confirmation that their request was submitted
                    if (customerId.HasValue)
                        await _notificationService.CreateAsync(
                            userId:  customerId.Value,
                            title:   "After-Sales Request Submitted",
                            message: $"Your {serviceLabel} request for Order #{orderId} from {shopName} " +
                                     $"has been submitted and is pending Customer Service approval."
                        );

                    // 2. Notify Seller — their order has an after-sales request pending
                    if (sellerId.HasValue)
                        await _notificationService.CreateAsync(
                            userId:  sellerId.Value,
                            title:   "After-Sales Request — Pending CS Approval",
                            message: $"Customer {customerName} has submitted a {serviceLabel} request " +
                                     $"for Order #{orderId}. Awaiting Customer Service approval."
                        );

                    // 3. Notify all Admins
                    var adminIds = await _context.Users
                        .Where(u => u.Role == "Admin" && u.IsActive)
                        .Select(u => u.UserId)
                        .ToListAsync();
                    foreach (var adminId in adminIds)
                        await _notificationService.CreateAsync(
                            userId:  adminId,
                            title:   "After-Sales Request — Pending CS Approval",
                            message: $"Order #{orderId} — {customerName} submitted a {serviceLabel} request " +
                                     $"from {shopName}. Total: RM{total:F2}. Awaiting Customer Service approval."
                        );

                    // 4. Notify all CustomerService users — action required
                    var csUserIds = await _context.Users
                        .Where(u => u.Role == "CustomerService" && u.IsActive)
                        .Select(u => u.UserId)
                        .ToListAsync();
                    foreach (var csUserId in csUserIds)
                        await _notificationService.CreateAsync(
                            userId:  csUserId,
                            title:   $"New {serviceLabel} Request — Action Required",
                            message: $"Customer {customerName} has submitted a {serviceLabel} request " +
                                     $"for Order #{orderId} at {shopName}. " +
                                     $"Total: RM{total:F2}. Please review and approve or reject."
                        );
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[RequestController] Submission notification failed for order #{orderId}: {ex.Message}");
            }

            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetRequest(int orderId)
        {
            var request = await _context.Request
                .Include(r => r.Images)
                .FirstOrDefaultAsync(r => r.OrderId == orderId);

            if (request == null)
                return Json(new { success = false, message = "Request not found" });

            var order = await _context.Order
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
                return Json(new { success = false, message = "Order not found" });

            // Calculate voucher discount ratio (same logic as seller dashboard)
            var rawSubtotal = order.OrderItems.Sum(oi => oi.Price * oi.Quantity);
            var voucherDiscount = Math.Max(0m, rawSubtotal - order.TotalAmount);
            var discRatio = rawSubtotal > 0 ? voucherDiscount / rawSubtotal : 0m;

            var requestedQtyMap = new Dictionary<int, int>(); // orderItemId -> requestedQty
            if (!string.IsNullOrWhiteSpace(request.RequestedItemsJson))
            {
                try
                {
                    var selections = System.Text.Json.JsonSerializer
                        .Deserialize<List<System.Text.Json.JsonElement>>(request.RequestedItemsJson);
                    if (selections != null)
                        foreach (var s in selections)
                            if (s.TryGetProperty("orderItemId", out var idEl)
                                && s.TryGetProperty("qty", out var qtyEl)) 
                                requestedQtyMap[idEl.GetInt32()] = qtyEl.GetInt32();
                }
                catch { /* fallback to full qty below */ }
            }

            return Json(new
            {
                success = true,
                customerName = order.Customer?.FullName ?? "Customer",
                orderItems = order.OrderItems
                    .Where(oi => requestedQtyMap.ContainsKey(oi.OrderItemId))   // only items the customer selected
                    .Select(oi => new
                    {
                        productName       = oi.Product?.Name,
                        quantity          = oi.Quantity,
                        requestedQty      = requestedQtyMap[oi.OrderItemId],    // safe — key guaranteed by Where
                        price             = oi.Price,
                        discountedPrice   = Math.Round(oi.Price * (1 - discRatio), 2),
                        imageUrl          = oi.Product?.ImagePath != null ? "/images/" + Path.GetFileName(oi.Product.ImagePath) : null,
                        selectedVariation = oi.SelectedVariation ?? ""          // e.g. {"Flavour":"Honey Tapioca","Size":"120g"}
                    }).ToList(),
                serviceType = request.RequestServiceType switch
                {
                    RequestServiceType.RETURN_REFUND => "Return & Refund",
                    RequestServiceType.REFUND => "Refund Only",
                    RequestServiceType.SUSPEND_ACCOUNT => "Suspend Account",
                    _ => "Other"
                },
                issueType = request.RequestIssueType switch
                {
                    RequestIssueType.WrongItemReceived => "Wrong item received",
                    RequestIssueType.ChangeOfMind => "Change of mind",
                    RequestIssueType.ItemNotAsDescribed => "Item not as described",
                    RequestIssueType.ItemNotDelivered => "Item not delivered",
                    RequestIssueType.DamagedDefective => "Damaged / defective",
                    RequestIssueType.MissingPartsAccessories => "Missing parts / accessories",
                    _ => "Other"
                },
                description          = request.Description,
                createdAt            = request.CreatedAt.ToString("dd MMM yyyy, hh:mm tt"),
                approvedAt           = request.ApprovedAt.HasValue
                                        ? request.ApprovedAt.Value.ToLocalTime().ToString("dd MMM yyyy, hh:mm tt")
                                        : (string?)null,
                approvedRefundAmount = order.ApprovedRefundAmount > 0
                                        ? order.ApprovedRefundAmount.ToString("F2")
                                        : (string?)null,
                returnType           = order.ReturnType.HasValue
                                        ? (order.ReturnType == EcommerceSystem.Enums.ReturnType.ReturnRefund
                                            ? "Return & Refund"
                                            : "Refund Only")
                                        : (string?)null,
                images               = request.Images.Select(img => "/uploads/" + img.ImagePath).ToList(),
                requestId            = request.RequestId
            });
        }

        [HttpPost]
        public IActionResult ApproveRequest(int requestId)
        {
            var request = _context.Request
                .Include(r => r.Order)
                .FirstOrDefault(r => r.RequestId == requestId);

            if (request == null) return NotFound();

            IRequestStrategy strategy;

            if (request.RequestServiceType == RequestServiceType.RETURN_REFUND)
            {
                strategy = new ReturnRefundStrategy();
            }
            else if (request.RequestServiceType == RequestServiceType.REFUND)
            {
                strategy = new RefundStrategy();
            }
            else if (request.RequestServiceType == RequestServiceType.SUSPEND_ACCOUNT)
            {
                strategy = new SuspendAccountStrategy();
            }
            else if (request.RequestServiceType == RequestServiceType.ACTIVE_ACCOUNT)
            {
                strategy = new ActivateAccountStrategy();
            }
            else
            {
                return BadRequest("Invalid request type for approval.");
            }

            strategy.Solve(request);
            request.ApprovedAt = DateTime.UtcNow;
            // request.ReviewedBy = User.Identity?.Name;
            _context.SaveChanges();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> RejectAfterSale(int requestId, int orderId)
        {
            var request = await _context.Request
                .Include(r => r.Order)
                .FirstOrDefaultAsync(r => r.RequestId == requestId);

            if (request == null) return NotFound();

            var order = await _context.Order
                .Include(o => o.Customer)
                .Include(o => o.Seller)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null) return NotFound();

            order.CurrentStatus = OrderStatus.DELIVERED;

            await _context.SaveChangesAsync();

            // ── Notify Customer that their after-sales request was rejected ────
            try
            {
                var rejectedRequest = await _context.Request
                    .FirstOrDefaultAsync(r => r.RequestId == requestId);

                var shopName     = order.Seller?.ShopName ?? "the seller";
                var customerId   = order.Customer?.UserId;
                var serviceLabel = rejectedRequest?.RequestServiceType == RequestServiceType.RETURN_REFUND
                    ? "Return & Refund" : "Refund Only";

                if (customerId.HasValue)
                    await _notificationService.CreateAsync(
                        userId:  customerId.Value,
                        title:   "After-Sales Request Rejected",
                        message: $"Your {serviceLabel} request for Order #{orderId} from {shopName} " +
                                 $"has been reviewed and rejected by Customer Service. " +
                                 $"Your order status has been restored to Delivered."
                    );
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[RequestController] Rejection notification failed for order #{orderId}: {ex.Message}");
            }

            return Json(new { success = true });
        }
    }
}