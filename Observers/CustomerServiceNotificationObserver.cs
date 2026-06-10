using EcommerceSystem.Data;
using EcommerceSystem.Interfaces;
using EcommerceSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace EcommerceSystem.Observers
{
    /// <summary>
    /// Sends notifications to ALL Customer Service users whenever a return/refund
    /// request is submitted by a customer, so they know a case needs their review.
    ///
    /// Trigger map:
    ///   RETURN_REFUND  → "New Return & Refund Request — Action Required"
    ///   REFUND         → "New Refund-Only Request — Action Required"
    ///
    /// All other order status changes are ignored by this observer.
    /// </summary>
    public class CustomerServiceNotificationObserver : OrderStatusObserver
    {
        private readonly INotificationService _notificationService;
        private readonly AppDbContext _context;

        public CustomerServiceNotificationObserver(
            INotificationService notificationService,
            AppDbContext context)
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
                Console.Error.WriteLine(
                    $"[CustomerServiceNotificationObserver] Notification failed: {ex.Message}");
            }
        }

        private async Task UpdateAsync(Order order)
        {
            // Only notify CS when a return/refund request is raised.
            if (order.CurrentStatus != OrderStatus.RETURN_REFUND &&
                order.CurrentStatus != OrderStatus.REFUND)
                return;

            var orderId      = order.OrderId;
            var customerId   = order.Customer?.UserId.ToString()  ?? "N/A";
            var customerName = order.Customer?.FullName            ?? "Unknown Customer";
            var shopName     = order.Seller?.ShopName              ?? "Unknown Shop";
            var sellerId     = order.Seller?.UserId.ToString()     ?? "N/A";
            var total        = order.TotalAmount;

            var serviceType  = order.CurrentStatus == OrderStatus.REFUND
                ? "Refund Only"
                : "Return & Refund";

            var title   = $"New {serviceType} Request — Action Required";
            var message =
                $"Customer #{customerId} ({customerName}) has submitted a {serviceType} " +
                $"request for Order #{orderId} at {shopName} (Seller #{sellerId}). " +
                $"Total: RM{total:F2}. Please review and approve or reject this request.";

            // Fetch all active Customer Service user IDs and notify each one.
            var csUserIds = await _context.Users
                .Where(u => u.Role == "CustomerService" && u.IsActive)
                .Select(u => u.UserId)
                .ToListAsync();

            foreach (var csUserId in csUserIds)
            {
                await _notificationService.CreateAsync(
                    userId:  csUserId,
                    title:   title,
                    message: message
                );
            }
        }
    }
}
