using EcommerceSystem.Models;
using EcommerceSystem.Interfaces;

namespace EcommerceSystem.Observers;

public class CustomerDashboardObserver : OrderStatusObserver
{
    private readonly INotificationService _notificationService;

    public CustomerDashboardObserver(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public void Update(Order order)
    {
        RefreshOrderStatus(order);
    }

    private void RefreshOrderStatus(Order order)
    {
        var title = $"Order #{order.OrderId} Status Updated";
        var message = order.CurrentStatus switch
        {
            OrderStatus.PREPARING    => $"Great news! Your order #{order.OrderId} is now being prepared by the seller.",
            OrderStatus.SHIPPED      => $"Your order #{order.OrderId} has been shipped and is on its way!",
            OrderStatus.DELIVERED    => $"Your order #{order.OrderId} has been delivered. Please confirm receipt.",
            OrderStatus.RECEIVED     => $"You have confirmed receipt of order #{order.OrderId}. Thank you!",
            OrderStatus.CANCELED     => $"Your order #{order.OrderId} has been cancelled.",
            OrderStatus.RETURN_REFUND => $"Your return/refund request for order #{order.OrderId} has been submitted.",
            _                        => $"Your order #{order.OrderId} status is now: {order.CurrentStatus}."
        };

        // Fire-and-forget: observer is sync but CreateAsync is async.
        // We use GetAwaiter().GetResult() here because the Observer interface
        // is synchronous. Consider making the interface async in a future refactor.
        _notificationService.CreateAsync(
            userId:  order.CustomerUserId,
            title:   title,
            message: message,
            type:    "OrderStatus",
            orderId: order.OrderId
        ).GetAwaiter().GetResult();
    }
}