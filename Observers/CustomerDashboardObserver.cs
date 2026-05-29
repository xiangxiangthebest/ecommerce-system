using System;
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
    ///   RETURN_REFUND  → "Return & Refund Requested"
    /// </summary>
    public class CustomerDashboardObserver : OrderStatusObserver
    {
        private readonly INotificationService _notificationService;

        public CustomerDashboardObserver(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        public async void Update(Order order)
        {
            try
            {
                await UpdateAsync(order);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[CustomerDashboardObserver] Notification failed: {ex.Message}");
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
                    title = "Return & Refund Requested";
                    message =
                        $"Your return/refund request for Order #{orderId} from {shopName} " +
                        $"has been submitted and is awaiting seller approval.";
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
