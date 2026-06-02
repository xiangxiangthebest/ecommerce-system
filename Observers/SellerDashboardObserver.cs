using EcommerceSystem.Interfaces;
using EcommerceSystem.Models;

namespace EcommerceSystem.Observers
{
    /// <summary>
    /// Sends notifications to the SELLER whenever one of their orders changes status.
    ///
    /// Trigger map:
    ///   PENDING        → "New Order Alert"      (customer just placed the order)
    ///   PREPARING      → "Processing Order"     (seller accepted — confirmation echo)
    ///   SHIPPED        → "Order Shipped"        (seller marked as shipped)
    ///   DELIVERED      → "Order Delivered"
    ///   RECEIVED       → "Order Received"       (customer confirmed receipt)
    ///   CANCELED       → "Order Cancelled"      (customer cancelled)
    ///   RETURN_REFUND  → "Return & Refund Requested"
    /// </summary>
    public class SellerDashboardObserver : OrderStatusObserver
    {
        private readonly INotificationService _notificationService;

        public SellerDashboardObserver(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        public async Task Update(Order order)
        {
            try
            {
                Console.WriteLine($"[SellerDashboardObserver] Update called for Order #{order.OrderId}, Status: {order.CurrentStatus}");
                if (order.Seller == null)
                {
                    Console.WriteLine($"[SellerDashboardObserver] Seller is NULL for Order #{order.OrderId}");
                    return;
                }
                Console.WriteLine($"[SellerDashboardObserver] Seller found: UserId={order.Seller.UserId}, ShopName={order.Seller.ShopName}");

                var sellerId    = order.Seller.UserId;
                var orderId     = order.OrderId;
                var customerId  = order.Customer?.UserId.ToString()   ?? "N/A";
                var customerName= order.Customer?.FullName             ?? "Unknown Customer";
                var shopName    = order.Seller.ShopName;
                var total       = order.TotalAmount;

                // Build a comma-separated product list from the order items (if loaded).
                var productList = BuildProductList(order);

                string title;
                string message;

                switch (order.CurrentStatus)
                {
                    // ── New order incoming ─────────────────────────────────────────
                    case OrderStatus.PENDING:
                        title = "🛒 New Order Alert";
                        message =
                            $"Order #{orderId}\n" +
                            $"Customer ID: {customerId}\n" +
                            $"Customer Name: {customerName}\n" +
                            $"Product(s) Ordered: {productList}\n" +
                            $"Total: RM{total:F2}";
                        break;

                    // ── Seller accepted / is now preparing ────────────────────────
                    case OrderStatus.PREPARING:
                        title = "Processing Order";
                        message =
                            $"Order #{orderId} is being processed.";
                        break;

                    // ── Seller handed to courier ───────────────────────────────────
                    case OrderStatus.SHIPPED:
                        title = "Order Shipped";
                        message =
                            $"Order #{orderId} is being shipped.";
                        break;

                    // ── Delivered to address ───────────────────────────────────────
                    case OrderStatus.DELIVERED:
                        title = "Order Delivered";
                        message =
                            $"Order #{orderId} has been delivered to " +
                            $"Customer #{customerId} ({customerName}).";
                        break;

                    // ── Customer confirmed receipt ─────────────────────────────────
                    case OrderStatus.RECEIVED:
                        title = "Order Received";
                        message =
                            $"Customer #{customerId} ({customerName}) has confirmed receipt " +
                            $"of Order #{orderId}. Transaction complete.";
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
                        title = "Return & Refund Requested";
                        message =
                            $"Customer #{customerId} ({customerName}) has requested a " +
                            $"return/refund for Order #{orderId}. Awaiting for Customer Service's response.";
                        break;

                    default:
                        return;
                }

                Console.WriteLine($"[SellerDashboardObserver] Creating notification for UserId={sellerId}, Title={title}");
                await _notificationService.CreateAsync(
                    userId:  sellerId,
                    title:   title,
                    message: message
                );
                Console.WriteLine($"[SellerDashboardObserver] Notification created successfully");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SellerDashboardObserver] Notification failed: {ex.Message}");
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
                    var name = oi.Product.Name;
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