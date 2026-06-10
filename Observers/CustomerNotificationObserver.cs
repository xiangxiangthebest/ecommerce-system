using EcommerceSystem.Interfaces;
using EcommerceSystem.Models;

namespace EcommerceSystem.Observers
{
    /// <summary>
    /// Sends notifications to the CUSTOMER whenever their order status changes.
    ///
    /// Trigger map:
    ///   PENDING        → "Order Placed"
    ///   PREPARING      → "Processing Order"
    ///   SHIPPED        → "Order Shipped"
    ///   DELIVERED      → "Order Delivered"
    ///   RECEIVED       → (no customer notification — they triggered it)
    ///   CANCELED       → "Order Cancelled"
    ///   RETURN_REFUND  → "Return & Refund Request Submitted" (pending CS approval)
    /// </summary>
    public class CustomerNotificationObserver : OrderStatusObserver
    {
        private readonly INotificationService _notificationService;

        public CustomerNotificationObserver(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        public async Task Update(Order order)
        {
            try
            {
                await UpdateAsync(order);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[CustomerNotificationObserver] Notification failed: {ex.Message}");
            }
        }

        private async Task UpdateAsync(Order order)
        {
            if (order.Customer == null) return;

            var customerId  = order.Customer.UserId;
            var orderId     = order.OrderId;
            var shopName    = order.Seller?.ShopName ?? "the seller";
            var total       = order.TotalAmount;

            // Build a comma-separated product list from the order items (if loaded).
            var productList = BuildProductList(order);

            string title;
            string message;

            switch (order.CurrentStatus)
            {
                // ── Customer places the order ──────────────────────────────────
                case OrderStatus.PENDING:
                    title = "Order Placed";
                    message =
                        $"Order #{orderId}\n" +
                        $"Shop: {shopName}\n" +
                        $"Product(s): {productList}\n" +
                        $"Total: RM{total:F2}\n" +
                        $"Status: Pending";
                    break;

                // ── Seller starts preparing ────────────────────────────────────
                case OrderStatus.PREPARING:
                    title = "Processing Order";
                    message =
                        $"Order #{orderId} from {shopName} is being processed.";
                    break;

                // ── Seller hands to courier ────────────────────────────────────
                case OrderStatus.SHIPPED:
                    title = "Order Shipped";
                    message =
                        $"Order #{orderId} from {shopName} is being shipped.";
                    break;

                // ── Courier marks delivered ────────────────────────────────────
                case OrderStatus.DELIVERED:
                    title = "Order Delivered";
                    message =
                        $"Order #{orderId} from {shopName} has been delivered. " +
                        $"Please confirm receipt when you have it.";
                    break;

                // ── Customer cancelled ─────────────────────────────────────────
                case OrderStatus.CANCELED:
                    title = "Order Cancelled";
                    message =
                        $"Order #{orderId}\n" +
                        $"Shop: {shopName}\n" +
                        $"Product(s): {productList}\n" +
                        $"Total: RM{total:F2}\n" +
                        $"has been cancelled.";
                    break;

                // ── Return / refund requested ──────────────────────────────────
                case OrderStatus.RETURN_REFUND:
                case OrderStatus.REFUND:
                    title = "Return & Refund Request Submitted";
                    message =
                        $"Your return/refund request for Order #{orderId} from {shopName} " +
                        $"has been submitted and is pending Customer Service approval.";
                    break;

                // RECEIVED — customer triggered this themselves; skip notification.
                default:
                    return;
            }

            await _notificationService.CreateAsync(
                userId:  customerId,
                title:   title,
                message: message
            );
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
                    var name = oi.Product?.Name ?? "Unknown product";
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