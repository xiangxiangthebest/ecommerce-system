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

        public async void Update(Order order)
        {
            if (order.Seller == null) return;

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

            await _notificationService.CreateAsync(
                userId:  sellerId,
                title:   title,
                message: message
            );
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string BuildProductList(Order order)
        {
            if (order.OrderItems == null || !order.OrderItems.Any())
                return "N/A";

            var names = order.OrderItems
                .Where(oi => oi.Product != null)
                .Select(oi => oi.Product.Name)
                .Distinct()
                .ToList();

            return names.Any() ? string.Join(", ", names) : "N/A";
        }
    }
}
