using EcommerceSystem.Data;
using EcommerceSystem.Interfaces;
using EcommerceSystem.Models;
using Microsoft.EntityFrameworkCore;

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

                        // stock decrement
                        item.Product!.StockQuantity -= item.Quantity;

                        // remove from cart if from cart
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
                .FirstOrDefaultAsync(o => o.OrderId == orderId
                                       && o.CustomerUserId == customerId
                                       && o.CurrentStatus == OrderStatus.PENDING);

            if (order == null)
                return OperationResult.Fail("Order cannot be canceled.");

            order.CurrentStatus = OrderStatus.CANCELED;
            order.CancelReason = reason.Trim();
            order.CanceledAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _notificationService.CreateAsync(
                order.CustomerUserId,
                "Order Cancelled",
                $"Your order #{order.OrderId} has been cancelled.",
                "OrderCancelled",
                order.OrderId
            );

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

            await _notificationService.CreateAsync(
                order.CustomerUserId,
                "Review Reminder",
                $"Please review your order #{order.OrderId}.",
                "ReviewReminder",
                order.OrderId
            );

            return OperationResult.Ok();
        }

        public async Task<OperationResult> SubmitComplaintAsync(int customerId, int orderId, string complaintText)
        {
            if (string.IsNullOrWhiteSpace(complaintText))
                return OperationResult.Fail("Please describe your complaint.");

            var order = await _context.Order
                .FirstOrDefaultAsync(o => o.OrderId == orderId
                                       && o.CustomerUserId == customerId
                                       && (o.CurrentStatus == OrderStatus.RECEIVED
                                           || o.CurrentStatus == OrderStatus.RETURN_REFUND));

            if (order == null)
                return OperationResult.Fail("Order not eligible for complaint.");

            order.ComplaintText = complaintText.Trim();
            order.ComplaintSubmitted = true;
            order.ComplaintAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return OperationResult.Ok();
        }

        public async Task<OperationResult> UpdateOrderStatusAsync(int orderId, OrderStatus status)
        {
            var order = await _context.Order
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
                return OperationResult.Fail("Order not found.");

            order.CurrentStatus = status;

            await _context.SaveChangesAsync();

            // =========================
            // NOTIFICATIONS
            // =========================

            if (status == OrderStatus.PREPARING)
            {
                await _notificationService.CreateAsync(
                    order.CustomerUserId,
                    "Order Preparing",
                    $"Your order #{order.OrderId} is being prepared.",
                    "OrderPreparing",
                    order.OrderId
                );
            }

            else if (status == OrderStatus.SHIPPED)
            {
                await _notificationService.CreateAsync(
                    order.CustomerUserId,
                    "Order Shipped",
                    $"Your order #{order.OrderId} has been shipped.",
                    "OrderShipped",
                    order.OrderId
                );
            }

            else if (status == OrderStatus.DELIVERED)
            {
                await _notificationService.CreateAsync(
                    order.CustomerUserId,
                    "Order Delivered",
                    $"Your order #{order.OrderId} has been delivered.",
                    "OrderDelivered",
                    order.OrderId
                );
            }

            return OperationResult.Ok();
        }
    }
}