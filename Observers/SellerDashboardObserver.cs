using EcommerceSystem.Models;
using EcommerceSystem.Interfaces;

namespace EcommerceSystem.Observers;

public class SellerDashboardObserver : OrderStatusObserver
{
    private readonly INotificationService _notificationService;

    public SellerDashboardObserver(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public void Update(Order order)
    {
        RefreshSellerOrder(order);
    }

    private void RefreshSellerOrder(Order order)
    {
        var title = $"Order #{order.OrderId} Updated";
        var message = order.CurrentStatus switch
        {
            OrderStatus.PREPARING     => $"You have accepted order #{order.OrderId}. Please prepare it for shipping.",
            OrderStatus.SHIPPED       => $"Order #{order.OrderId} has been marked as shipped.",
            OrderStatus.DELIVERED     => $"Order #{order.OrderId} has been delivered to the customer.",
            OrderStatus.CANCELED      => $"Order #{order.OrderId} from your shop has been cancelled.",
            OrderStatus.RETURN_REFUND => $"The customer has raised a return/refund request for order #{order.OrderId}. Please review it.",
            OrderStatus.RECEIVED      => $"The customer has confirmed receipt of order #{order.OrderId}. The order is complete.",
            _                         => $"Order #{order.OrderId} is now: {order.CurrentStatus}."
        };

        _notificationService.CreateAsync(
            userId:  order.SellerUserId,
            title:   title,
            message: message,
            type:    "OrderStatus",
            orderId: order.OrderId
        ).GetAwaiter().GetResult();
    }
}