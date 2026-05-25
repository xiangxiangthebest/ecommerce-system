using EcommerceSystem.Data;
using EcommerceSystem.Interfaces;
using EcommerceSystem.Models;
using Microsoft.EntityFrameworkCore;
using EcommerceSystem.Enums;
using System.Text.Json;

namespace EcommerceSystem.Services
{
    public class OrderService : IOrderService
    {
        private readonly AppDbContext _context;

        public OrderService(AppDbContext context)
        {
            _context = context;
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

        public async Task<OperationResult> PlaceOrderAsync(PlaceOrderRequest request)
        {
            var address = await _context.DeliveryField
                .FirstOrDefaultAsync(a => a.AddressId == request.SelectedAddressId && a.UserId == request.CustomerId);

            if (address == null) return OperationResult.Fail("Invalid address.");

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
                        PaymentMethod = request.PaymentMethod,
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
                }

                await tx.CommitAsync();
                return OperationResult.Ok();
            }
            catch
            {
                await tx.RollbackAsync();
                return OperationResult.Fail("Failed to place order. Please try again.");
            }
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
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderId == orderId
                                       && o.CustomerUserId == customerId
                                       && o.CurrentStatus == OrderStatus.PENDING);

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

            order.CurrentStatus = OrderStatus.CANCELED;
            order.CancelReason = reason.Trim();
            order.CanceledAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return OperationResult.Ok();
        }

        public async Task<OperationResult> ConfirmReceivedAsync(int customerId, int orderId)
        {
            var order = await _context.Order
                .FirstOrDefaultAsync(o => o.OrderId == orderId
                                       && o.CustomerUserId == customerId
                                       && o.CurrentStatus == OrderStatus.DELIVERED);

            if (order == null)
                return OperationResult.Fail("Order not found or already confirmed.");

            order.CurrentStatus = OrderStatus.RECEIVED;
            order.ReceivedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return OperationResult.Ok();
        }

        public async Task<OperationResult> RequestReturnRefundAsync(int userId, int orderId, string reason, List<string> imagePaths, ReturnInitiatedBy initiatedBy)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return OperationResult.Fail("Please provide a return/refund reason.");

            var order = await _context.Order
                .FirstOrDefaultAsync(o =>
                    o.OrderId == orderId &&
                    o.CustomerUserId == userId);

            if (order == null)
                return OperationResult.Fail("Order not found.");

            if (order.CurrentStatus != OrderStatus.DELIVERED)
                return OperationResult.Fail("Only delivered orders can request return/refund.");

            order.ReturnRequested = true;
            order.ReturnStatus = ReturnStatus.Requested;
            order.ReturnReason = reason.Trim();
            order.ReturnImagePathsJson = imagePaths.Any() ? JsonSerializer.Serialize(imagePaths) : null;
            order.ReturnInitiatedAt = DateTime.UtcNow;
            order.ReturnInitiatedBy = initiatedBy;

            order.CurrentStatus = OrderStatus.RETURN_REFUND;

            await _context.SaveChangesAsync();

            return OperationResult.Ok();
        }
    }
}