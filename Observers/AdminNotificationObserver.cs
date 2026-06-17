using EcommerceSystem.Data;
using EcommerceSystem.Interfaces;
using EcommerceSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace EcommerceSystem.Observers
{
    public class AdminNotificationObserver : OrderStatusObserver
    {
        private readonly INotificationService _notificationService;
        private readonly AppDbContext _context;

        public AdminNotificationObserver(INotificationService notificationService, AppDbContext context)
        {
            _notificationService = notificationService;
            _context = context;
        }

        public async Task Update(Order order)
        {
            try
            {
                await UpdateAsync(order);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[AdminNotificationObserver] Notification failed: {ex.Message}");
            }
        }

        private async Task UpdateAsync(Order order)
        {
            var orderId      = order.OrderId;
            var customerId   = order.Customer?.UserId.ToString()   ?? "N/A";
            var customerName = order.Customer?.FullName             ?? "Unknown Customer";
            var sellerId     = order.Seller?.UserId.ToString()      ?? "N/A";
            var shopName     = order.Seller?.ShopName               ?? "Unknown Shop";
            var total        = order.TotalAmount;
            var productList  = BuildProductList(order);

            string title;
            string message;

            switch (order.CurrentStatus)
            {
                case OrderStatus.PENDING:
                    title = "New Order Placed";
                    message =
                        $"Customer #{customerId} ({customerName}) has placed an order " +
                        $"at {shopName} (Seller #{sellerId}). " +
                        $"Product(s): {productList}. Total: RM{total:F2}";
                    break;

                case OrderStatus.PREPARING:
                    title = "Order Being Processed";
                    message =
                        $"Order #{orderId} — {shopName} (Seller #{sellerId}) is now " +
                        $"processing the order for Customer #{customerId} ({customerName}).";
                    break;

                case OrderStatus.SHIPPED:
                    title = "Order Shipped";
                    message =
                        $"Order #{orderId} from {shopName} (Seller #{sellerId}) " +
                        $"has been shipped to Customer #{customerId} ({customerName}).";
                    break;

                case OrderStatus.DELIVERED:
                    title = "Order Delivered";
                    message =
                        $"Order #{orderId} from {shopName} has been delivered to " +
                        $"Customer #{customerId} ({customerName}).";
                    break;

                case OrderStatus.RECEIVED:
                    title = "Order Received by Customer";
                    message =
                        $"Customer #{customerId} ({customerName}) has confirmed receipt " +
                        $"of Order #{orderId} from {shopName}. Transaction complete.";
                    break;

                case OrderStatus.CANCELED:
                    title = "Order Cancelled";
                    message =
                        $"Order #{orderId} — Customer #{customerId} ({customerName}) " +
                        $"has cancelled their order at {shopName} (Seller #{sellerId}). " +
                        $"Product(s): {productList}. Total: RM{total:F2}";
                    break;

                case OrderStatus.RETURN_REFUND:
                case OrderStatus.REFUND:
                    title = "Return & Refund Request — Return/Refund Requested";
                    message =
                        $"Order #{orderId} — Customer #{customerId} ({customerName}) " +
                        $"has submitted a return/refund request from {shopName} (Seller #{sellerId}). " +
                        $"Total: RM{total:F2}. Awaiting Customer Service approval.";
                    break;

                default:
                    return;
            }

            var adminIds = await _context.Users
                .Where(u => u.Role == "Admin" && u.IsActive)
                .Select(u => u.UserId)
                .ToListAsync();

            foreach (var adminId in adminIds)
            {
                await _notificationService.CreateAsync(
                    userId:  adminId,
                    title:   title,
                    message: message,
                    orderId: order.OrderId
                );
            }
        }

        private static string BuildProductList(Order order)
        {
            if (order.OrderItems == null || !order.OrderItems.Any())
                return "N/A";

            var entries = order.OrderItems
                .Where(oi => oi.Product != null)
                .Select(oi =>
                {
                    var name = oi.Product?.Name ?? "Unknown Product";
                    var variationSuffix = BuildVariationSuffix(oi.SelectedVariation);
                    return string.IsNullOrEmpty(variationSuffix)
                        ? name
                        : $"{name} ({variationSuffix})";
                })
                .ToList();

            return entries.Any() ? string.Join(", ", entries) : "N/A";
        }
        
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