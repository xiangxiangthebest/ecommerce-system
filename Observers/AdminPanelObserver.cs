using EcommerceSystem.Data;
using EcommerceSystem.Interfaces;
using EcommerceSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace EcommerceSystem.Observers
{
    /// <summary>
    /// Sends notifications to ALL Admin users whenever any order status changes.
    ///
    /// Admins see every event:
    ///   PENDING        → Customer placed order
    ///   PREPARING      → Seller is processing
    ///   SHIPPED        → Order shipped
    ///   DELIVERED      → Order delivered
    ///   RECEIVED       → Customer confirmed receipt
    ///   CANCELED       → Order cancelled
    ///   RETURN_REFUND  → Return / refund requested
    /// </summary>
    public class AdminPanelObserver : OrderStatusObserver
    {
        private readonly INotificationService _notificationService;
        private readonly AppDbContext _context;

        public AdminPanelObserver(INotificationService notificationService, AppDbContext context)
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
                Console.Error.WriteLine($"[AdminPanelObserver] Notification failed: {ex.Message}");
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
                // ── Customer places an order ───────────────────────────────────
                case OrderStatus.PENDING:
                    title = "New Order Placed";
                    message =
                        $"Customer #{customerId} ({customerName}) has placed an order " +
                        $"at {shopName} (Seller #{sellerId}). " +
                        $"Product(s): {productList}. Total: RM{total:F2}";
                    break;

                // ── Seller accepts / starts preparing ─────────────────────────
                case OrderStatus.PREPARING:
                    title = "Order Being Processed";
                    message =
                        $"Order #{orderId} — {shopName} (Seller #{sellerId}) is now " +
                        $"processing the order for Customer #{customerId} ({customerName}).";
                    break;

                // ── Seller ships the order ─────────────────────────────────────
                case OrderStatus.SHIPPED:
                    title = "Order Shipped";
                    message =
                        $"Order #{orderId} from {shopName} (Seller #{sellerId}) " +
                        $"has been shipped to Customer #{customerId} ({customerName}).";
                    break;

                // ── Courier delivers to address ────────────────────────────────
                case OrderStatus.DELIVERED:
                    title = "Order Delivered";
                    message =
                        $"Order #{orderId} from {shopName} has been delivered to " +
                        $"Customer #{customerId} ({customerName}).";
                    break;

                // ── Customer confirms receipt ──────────────────────────────────
                case OrderStatus.RECEIVED:
                    title = "Order Received by Customer";
                    message =
                        $"Customer #{customerId} ({customerName}) has confirmed receipt " +
                        $"of Order #{orderId} from {shopName}. Transaction complete.";
                    break;

                // ── Customer cancels ───────────────────────────────────────────
                case OrderStatus.CANCELED:
                    title = "Order Cancelled";
                    message =
                        $"Order #{orderId} — Customer #{customerId} ({customerName}) " +
                        $"has cancelled their order at {shopName} (Seller #{sellerId}). " +
                        $"Product(s): {productList}. Total: RM{total:F2}";
                    break;

                // ── Return / refund requested ──────────────────────────────────
                // case OrderStatus.RETURN_REFUND:
                //     title = "Return & Refund Requested";
                //     message =
                //         $"Order #{orderId} — Customer #{customerId} ({customerName}) " +
                //         $"has requested a return/refund from {shopName} (Seller #{sellerId}). " +
                //         $"Total: RM{total:F2}";
                //     break;

                default:
                    return;
            }

            // Fetch all admin user IDs from the database and notify each one.
            var adminIds = await _context.Users
                .Where(u => u.Role == "Admin" && u.IsActive)
                .Select(u => u.UserId)
                .ToListAsync();

            foreach (var adminId in adminIds)
            {
                await _notificationService.CreateAsync(
                    userId:  adminId,
                    title:   title,
                    message: message
                );
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        // ── Builds a product list with variation details ─────────────────────
        // Example output: "Bika (Flavour: Honey), Bika (Flavour: Original, Size: Large)"
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

        // Parses SelectedVariation JSON like {"Flavour":"Honey","Size":"Large"}
        // and returns a readable string like "Flavour: Honey, Size: Large".
        // Returns empty string if there are no variations or JSON is empty/invalid.
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