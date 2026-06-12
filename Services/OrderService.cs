using EcommerceSystem.Data;
using EcommerceSystem.Interfaces;
using EcommerceSystem.Observers;
using EcommerceSystem.Models;
using Microsoft.EntityFrameworkCore;
using EcommerceSystem.Enums;
using System.Text.Json;

namespace EcommerceSystem.Services
{
    public class OrderService : IOrderService
    {
        private readonly AppDbContext _context;
        private readonly INotificationService _notificationService;

        public OrderService(AppDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        // ── Deducts stock from the matching combo inside VariationCombosJson ──
        // selectedVariationsJson example: {"Flavour":"Honey(60g)"}
        // combosJson example: [{"keys":["Honey(60g)"],"stock":250}]
        private static string DeductComboStock(string combosJson, string selectedVariationsJson, int quantity)
        {
            if (string.IsNullOrWhiteSpace(combosJson) || combosJson == "[]")
                return combosJson;

            try
            {
                var selected = JsonSerializer.Deserialize<Dictionary<string, string>>(selectedVariationsJson)
                               ?? new Dictionary<string, string>();

                if (selected.Count == 0) return combosJson;

                // Collect all selected variation values, e.g. ["Honey(60g)"]
                var selectedValues = new HashSet<string>(selected.Values, StringComparer.OrdinalIgnoreCase);

                var combos = JsonSerializer.Deserialize<List<JsonElement>>(combosJson);
                if (combos == null) return combosJson;

                var updated = new List<object>();
                foreach (var combo in combos)
                {
                    var keys = combo.TryGetProperty("keys", out var k)
                        ? k.EnumerateArray().Select(x => x.GetString() ?? "").ToList()
                        : new List<string>();

                    int stock = 0;
                    if (combo.TryGetProperty("stock", out var s))
                    {
                        stock = s.ValueKind == JsonValueKind.Number
                            ? (s.TryGetInt32(out var si) ? si : (int)s.GetDouble())
                            : int.TryParse(s.GetString(), out var sp) ? sp : 0;
                    }

                    // Match: every key in this combo must appear in the selected values
                    bool isMatch = keys.Count > 0 && keys.All(key => selectedValues.Contains(key));

                    if (isMatch)
                        stock = Math.Max(0, stock - quantity);

                    updated.Add(new { keys, stock });
                }

                return JsonSerializer.Serialize(updated);
            }
            catch
            {
                return combosJson;
            }
        }

        // ── Adds quantity back to the matching combo inside VariationCombosJson ──
        // Mirror of DeductComboStock — used when an order is cancelled.
        private static string RestoreComboStock(string combosJson, string selectedVariationsJson, int quantity)
        {
            if (string.IsNullOrWhiteSpace(combosJson) || combosJson == "[]")
                return combosJson;

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

                    int stock = 0;
                    if (combo.TryGetProperty("stock", out var s))
                    {
                        stock = s.ValueKind == JsonValueKind.Number
                            ? (s.TryGetInt32(out var si) ? si : (int)s.GetDouble())
                            : int.TryParse(s.GetString(), out var sp) ? sp : 0;
                    }

                    bool isMatch = keys.Count > 0 && keys.All(key => selectedValues.Contains(key));

                    if (isMatch)
                        stock += quantity;  // add back instead of deduct

                    updated.Add(new { keys, stock });
                }

                return JsonSerializer.Serialize(updated);
            }
            catch
            {
                return combosJson;
            }
        }

        // ── Attaches all three observers to an order ──
        // Call this before order.SetStatus() so all parties are notified.
        private void AttachObservers(Order order)
        {
            order.Attach(new CustomerNotificationObserver(_notificationService));
            order.Attach(new SellerNotificationObserver(_notificationService));
            order.Attach(new AdminNotificationObserver(_notificationService, _context));
        }

        public async Task<OperationResult> PlaceOrderAsync(PlaceOrderRequest request)
        {
            var address = await _context.DeliveryField
                .FirstOrDefaultAsync(a => a.AddressId == request.SelectedAddressId && a.UserId == request.CustomerId);

            if (address == null) return OperationResult.Fail("Invalid address.");

            CustomerVoucher? appliedCustomerVoucher = null;
            decimal voucherDiscount = 0m;

            if (request.SelectedVoucherId.HasValue)
            {
                appliedCustomerVoucher = await _context.CustomerVouchers
                    .Include(cv => cv.Voucher)
                    .FirstOrDefaultAsync(cv => cv.CustomerVoucherId == request.SelectedVoucherId.Value && cv.CustomerId == request.CustomerId);

                if (appliedCustomerVoucher == null)
                    return OperationResult.Fail("Selected voucher is invalid.");

                if (appliedCustomerVoucher.IsUsed)
                    return OperationResult.Fail("Selected voucher has already been used.");

                var voucher = appliedCustomerVoucher.Voucher;
                if (voucher == null)
                    return OperationResult.Fail("Selected voucher is invalid.");

                if (!voucher.IsActive || voucher.StartDate > DateTime.Now || voucher.EndDate < DateTime.Now)
                    return OperationResult.Fail("Selected voucher is not valid at this time.");

                if (voucher.Quantity <= 0)
                    return OperationResult.Fail("Selected voucher is no longer available.");

                voucherDiscount = voucher.DiscountValue;
            }

            List<CartItem> itemsToPurchase;

            if (request.Source == "product")
            {
                if (!request.ProductId.HasValue) return OperationResult.Fail("Product not specified.");

                var product = await _context.Products
                    .Include(p => p.Seller)
                    .FirstOrDefaultAsync(p => p.ProductId == request.ProductId.Value);

                if (product == null) return OperationResult.Fail("Product not found.");

                var qty = (request.BuyNowQuantity ?? 1) <= 0 ? 1 : request.BuyNowQuantity!.Value;

                itemsToPurchase = new List<CartItem>
                {
                    new CartItem
                    {
                        CartItemId = 0,
                        ProductId = product.ProductId,
                        Product = product,
                        Quantity = qty,
                        Price = product.Price,
                        SelectedVariations = request.BuyNowSelectedVariations ?? "{}"
                    }
                };
            }
            else
            {
                if (request.SelectedItemIds == null || request.SelectedItemIds.Count == 0)
                    return OperationResult.Fail("No items selected.");

                itemsToPurchase = await _context.CartItem
                    .Include(ci => ci.Product)!.ThenInclude(p => p!.Seller)
                    .Include(ci => ci.Cart)
                    .Where(ci => request.SelectedItemIds.Contains(ci.CartItemId) && ci.Cart!.UserId == request.CustomerId)
                    .ToListAsync();

                if (!itemsToPurchase.Any()) return OperationResult.Fail("Your cart is empty.");
            }

            var groupedBySeller = itemsToPurchase.GroupBy(i => new { i.Product!.SellerId, i.Product.Seller!.ShopName });

            if (appliedCustomerVoucher != null)
            {
                var voucher = appliedCustomerVoucher.Voucher!;
                var totalCartAmount = groupedBySeller.Sum(group => group.Sum(i => i.Quantity * (decimal)i.Price));
                if (voucher.MinimumSpend.HasValue && totalCartAmount < voucher.MinimumSpend.Value)
                    return OperationResult.Fail($"This voucher requires a minimum spend of RM{voucher.MinimumSpend.Value:0.00}.");
            }

            decimal voucherDiscountRemaining = voucherDiscount;
            var placedOrderIds = new List<int>();

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var group in groupedBySeller)
                {
                    var sellerId = group.Key.SellerId;
                    var sellerName = group.Key.ShopName;

                    var messageKey = $"SellerMessage_{sellerName.Replace(" ", "_")}";
                    request.SellerMessages.TryGetValue(messageKey, out var customerMessage);

                    var orderTotal = group.Sum(i => i.Quantity * (decimal)i.Price);
                    decimal discountApplied = 0m;                                       
                    if (voucherDiscountRemaining > 0)
                    {
                        discountApplied = Math.Min(voucherDiscountRemaining, orderTotal); 
                        orderTotal -= discountApplied;
                        voucherDiscountRemaining -= discountApplied;
                    }

                    var order = new Order
                    {
                        CustomerUserId = request.CustomerId,
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
                        VoucherApplied = discountApplied > 0, 
                        PaymentMethod = request.PaymentMethod,
                        CurrentStatus = OrderStatus.PENDING,
                        OrderTime = DateTime.Now,
                        CustomerMessage = customerMessage
                    };

                    _context.Order.Add(order);
                    await _context.SaveChangesAsync();  // generates order.OrderId

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

                        // Deduct from total stock quantity
                        item.Product!.StockQuantity -= item.Quantity;

                        // ── Also deduct from the matching combo's stock in VariationCombosJson ──
                        var selectedVars = item.SelectedVariations ?? "{}";
                        if (!string.IsNullOrWhiteSpace(item.Product.VariationCombosJson)
                            && item.Product.VariationCombosJson != "[]"
                            && selectedVars != "{}")
                        {
                            item.Product.VariationCombosJson = DeductComboStock(
                                item.Product.VariationCombosJson,
                                selectedVars,
                                item.Quantity
                            );
                        }

                        // Remove from cart if purchased from cart
                        if (request.Source != "product" && item.CartItemId != 0)
                            _context.CartItem.Remove(item);
                    }

                    await _context.SaveChangesAsync();

                    placedOrderIds.Add(order.OrderId);
                }

                if (appliedCustomerVoucher != null)
                {
                    appliedCustomerVoucher.IsUsed = true;
                    appliedCustomerVoucher.UsedAt = DateTime.UtcNow;

                    if (appliedCustomerVoucher.Voucher != null && appliedCustomerVoucher.Voucher.Quantity > 0)
                        appliedCustomerVoucher.Voucher.Quantity -= 1;

                    await _context.SaveChangesAsync();
                }

                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                return OperationResult.Fail("Failed to place order. Please try again.");
            }

            // ── Send PENDING notifications AFTER the transaction has committed ──
            // Failures here never roll back the order — the purchase is already saved.
            //
            // WHY we call _notificationService.CreateAsync() directly instead of
            // going through the observer Update() methods:
            //
            //   1. SetStatus(PENDING) is a no-op because the order is already saved
            //      as PENDING — SetStatus() guards against same-status transitions.
            //
            //   2. The observer Update() methods are declared as `async void`, which
            //      means they cannot be awaited. Calling them fires-and-forgets on a
            //      background thread, so the DB write may never complete before the
            //      HTTP response returns, and any exception is silently swallowed.
            //
            // Calling CreateAsync() directly here ensures every notification is
            // properly awaited and any failure is caught and logged.
            foreach (var orderId in placedOrderIds)
            {
                try
                {
                    var o = await _context.Order
                        .Include(o => o.Customer)
                        .Include(o => o.Seller)
                        .Include(o => o.OrderItems)
                            .ThenInclude(oi => oi.Product)
                        .FirstOrDefaultAsync(o => o.OrderId == orderId);

                    if (o == null) continue;

                    var shopName    = o.Seller?.ShopName                  ?? "the seller";
                    var sellerId    = o.Seller?.UserId;
                    var sellerIdStr = o.Seller?.UserId.ToString()          ?? "N/A";
                    var customerId  = o.Customer?.UserId;
                    var customerName= o.Customer?.FullName                 ?? "Unknown Customer";
                    var customerIdStr = o.Customer?.UserId.ToString()      ?? "N/A";
                    var total       = o.TotalAmount;

                    // Build product list with variation details
                    // e.g. "Bika (Flavour: Honey), Bika (Flavour: Original, Size: Large)"
                    var productList = "N/A";
                    if (o.OrderItems != null && o.OrderItems.Any())
                    {
                        var entries = o.OrderItems
                            .Where(oi => oi.Product != null)
                            .Select(oi =>
                            {
                                var name = oi.Product?.Name ?? "Unknown Product";
                                var varSuffix = BuildVariationSuffix(oi.SelectedVariation);
                                return string.IsNullOrEmpty(varSuffix)
                                    ? name
                                    : $"{name} ({varSuffix})";
                            })
                            .ToList();
                        if (entries.Any())
                            productList = string.Join(", ", entries);
                    }

                    // ── Customer: "Order Placed" ───────────────────────────────
                    if (customerId.HasValue)
                    {
                        await _notificationService.CreateAsync(
                            userId:  customerId.Value,
                            title:   "Order Placed",
                            message: $"Order #{orderId}\n" +
                                     $"Shop: {shopName}\n" +
                                     $"Product(s): {productList}\n" +
                                     $"Total: RM{total:F2}\n" +
                                     $"Status: Pending"
                        );
                    }

                    // ── Seller: "New Order Alert" ──────────────────────────────
                    if (sellerId.HasValue)
                    {
                        await _notificationService.CreateAsync(
                            userId:  sellerId.Value,
                            title:   "🛒 New Order Alert",
                            message: $"Order #{orderId}\n" +
                                     $"Customer ID: {customerIdStr}\n" +
                                     $"Customer Name: {customerName}\n" +
                                     $"Product(s) Ordered: {productList}\n" +
                                     $"Total: RM{total:F2}"
                        );
                    }

                    // ── All Admins: "New Order Placed" ─────────────────────────
                    var adminIds = await _context.Users
                        .Where(u => u.Role == "Admin" && u.IsActive)
                        .Select(u => u.UserId)
                        .ToListAsync();

                    foreach (var adminId in adminIds)
                    {
                        await _notificationService.CreateAsync(
                            userId:  adminId,
                            title:   "New Order Placed",
                            message: $"Customer #{customerIdStr} ({customerName}) has placed an order " +
                                     $"at {shopName} (Seller #{sellerIdStr}). " +
                                     $"Product(s): {productList}. Total: RM{total:F2}"
                        );
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[OrderService] PENDING notification failed for order #{orderId}: {ex.Message}");
                }
            }

            return OperationResult.Ok();
        }

        public async Task<List<Order>> GetPurchaseHistoryAsync(int customerId)
        {
            var orders = await _context.Order
                .Where(o => o.CustomerUserId == customerId)
                .Include(o => o.OrderItems)!.ThenInclude(oi => oi.Product)!.ThenInclude(p => p!.Seller)
                .Include(o => o.Address)
                .OrderByDescending(o => o.OrderTime)
                .ToListAsync();

            // review submitted calculation (previous controller logic moved here)
            var orderItemIds = orders.SelectMany(o => o.OrderItems).Select(oi => oi.OrderItemId).ToList();

            if (orderItemIds.Count > 0)
            {
                var reviews = await _context.Reviews
                    .Where(r => r.CustomerId == customerId && orderItemIds.Contains(r.OrderItemId))
                    .ToListAsync();

                var reviewedSet = reviews.Select(r => r.OrderItemId).ToHashSet();
                var reviewMap   = reviews.ToDictionary(r => r.OrderItemId);

                foreach (var o in orders)
                {
                    o.ReviewSubmitted = o.OrderItems.Count > 0
                        && o.OrderItems.All(oi => reviewedSet.Contains(oi.OrderItemId));

                    foreach (var oi in o.OrderItems)
                        oi.Review = reviewMap.GetValueOrDefault(oi.OrderItemId);
                }
            }

            return orders;
        }

        public async Task<OperationResult> CancelOrderAsync(int customerId, int orderId, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return OperationResult.Fail("Please provide a cancellation reason.");

            var order = await _context.Order
                .Include(o => o.Customer)
                .Include(o => o.Seller)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.OrderId == orderId
                                       && o.CustomerUserId == customerId
                                       && o.CurrentStatus == OrderStatus.PENDING || o.CurrentStatus == OrderStatus.PREPARING);

            if (order == null)
                return OperationResult.Fail("Order cannot be canceled.");

            // ── Restore stock for every item in the cancelled order ──
            foreach (var item in order.OrderItems)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product == null) continue;

                // Restore total stock quantity
                product.StockQuantity += item.Quantity;

                // Also restore the matching combo stock in VariationCombosJson
                var selectedVars = item.SelectedVariation ?? "{}";
                if (!string.IsNullOrWhiteSpace(product.VariationCombosJson)
                    && product.VariationCombosJson != "[]"
                    && selectedVars != "{}")
                {
                    product.VariationCombosJson = RestoreComboStock(
                        product.VariationCombosJson,
                        selectedVars,
                        item.Quantity
                    );
                }
            }

            order.CancelReason = reason.Trim();
            order.CanceledAt = DateTime.UtcNow;

            // ── Notify all parties then persist ──
            AttachObservers(order);
            await order.SetStatusAsync(OrderStatus.CANCELED);

            await _context.SaveChangesAsync();

            return OperationResult.Ok();
        }
        
        public async Task<OperationResult> ConfirmReceivedAsync(int customerId, int orderId)
        {
            var order = await _context.Order
                .Include(o => o.Customer)
                .Include(o => o.Seller)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.OrderId == orderId
                    && o.CustomerUserId == customerId
                    && (o.CurrentStatus == OrderStatus.DELIVERED
                        || o.CurrentStatus == OrderStatus.RETURN_REFUND
                        || o.CurrentStatus == OrderStatus.REFUND));

            if (order == null)
                return OperationResult.Fail("Order not found or already confirmed.");
            order.ReceivedAt = DateTime.UtcNow;

            // ── Notify all parties then persist ──
            // For RETURN_REFUND / REFUND orders we set the status directly because
            // the state machine (Order.SetStatusAsync) may not define that transition.
            // Notifications are sent manually so Customer/Seller/Admin are still informed.
            if (order.CurrentStatus == OrderStatus.RETURN_REFUND
                || order.CurrentStatus == OrderStatus.REFUND)
            {
                order.CurrentStatus = OrderStatus.RECEIVED;
                // Send notifications directly (bypassing state machine)
                var customerObs = new CustomerNotificationObserver(_notificationService);
                var sellerObs   = new SellerNotificationObserver(_notificationService);
                var adminObs    = new AdminNotificationObserver(_notificationService, _context);
                await customerObs.Update(order);
                await sellerObs.Update(order);
                await adminObs.Update(order);
            }
            else
            {
                AttachObservers(order);
                await order.SetStatusAsync(OrderStatus.RECEIVED);
            }

            await _context.SaveChangesAsync();

            return OperationResult.Ok();
        }

        public async Task<OperationResult> RequestReturnRefundAsync(int userId, int orderId, string reason, List<string> imagePaths)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return OperationResult.Fail("Please provide a return/refund reason.");

            var order = await _context.Order
                .Include(o => o.Customer)
                .Include(o => o.Seller)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o =>
                    o.OrderId == orderId &&
                    o.CustomerUserId == userId);

            if (order == null)
                return OperationResult.Fail("Order not found.");

            if (order.CurrentStatus != OrderStatus.DELIVERED)
                return OperationResult.Fail("Only delivered orders can request return/refund.");

            // order.ReturnRequested = true;
            // order.ReturnStatus = ReturnStatus.Requested;
            // order.ReturnReason = reason.Trim();
            // order.ReturnImagePathsJson = imagePaths.Any() ? JsonSerializer.Serialize(imagePaths) : null;
            // order.ReturnInitiatedAt = DateTime.UtcNow;
            // order.ReturnInitiatedBy = initiatedBy;

            // ── Notify all parties then persist ──
            AttachObservers(order);
            await order.SetStatusAsync(OrderStatus.RETURN_REFUND);

            await _context.SaveChangesAsync();

            return OperationResult.Ok();
        }

        // Parses SelectedVariation JSON like {"Flavour":"Honey","Size":"Large"}
        // Returns "Flavour: Honey, Size: Large" or empty string if no variations.
        private static string BuildVariationSuffix(string? selectedVariationJson)
        {
            if (string.IsNullOrWhiteSpace(selectedVariationJson)
                || selectedVariationJson == "{}"
                || selectedVariationJson == "null")
                return string.Empty;

            try
            {
                var dict = System.Text.Json.JsonSerializer
                    .Deserialize<Dictionary<string, string>>(selectedVariationJson);

                if (dict == null || dict.Count == 0)
                    return string.Empty;

                return string.Join(", ", dict
                    .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                    .Select(kv => $"{kv.Key}: {kv.Value}"));
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}