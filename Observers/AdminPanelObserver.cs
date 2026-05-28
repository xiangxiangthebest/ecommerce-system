using EcommerceSystem.Models;
using EcommerceSystem.Interfaces;
using EcommerceSystem.Data;

namespace EcommerceSystem.Observers;

public class AdminPanelObserver : OrderStatusObserver
{
    private readonly INotificationService _notificationService;
    private readonly AppDbContext _context;

    public AdminPanelObserver(INotificationService notificationService, AppDbContext context)
    {
        _notificationService = notificationService;
        _context = context;
    }

    public void Update(Order order)
    {
        RefreshView(order);
    }

    private void RefreshView(Order order)
    {
        var title = $"[Admin] Order #{order.OrderId} — {order.CurrentStatus}";
        var message = order.CurrentStatus switch
        {
            OrderStatus.PREPARING     => $"Order #{order.OrderId} (RM{order.TotalAmount:F2}) is now being prepared by the seller.",
            OrderStatus.SHIPPED       => $"Order #{order.OrderId} (RM{order.TotalAmount:F2}) has been shipped.",
            OrderStatus.DELIVERED     => $"Order #{order.OrderId} (RM{order.TotalAmount:F2}) has been delivered.",
            OrderStatus.RECEIVED      => $"Order #{order.OrderId} (RM{order.TotalAmount:F2}) was confirmed received by the customer.",
            OrderStatus.CANCELED      => $"Order #{order.OrderId} (RM{order.TotalAmount:F2}) has been cancelled.",
            OrderStatus.RETURN_REFUND => $"Order #{order.OrderId} (RM{order.TotalAmount:F2}) has a return/refund request pending.",
            _                         => $"Order #{order.OrderId} (RM{order.TotalAmount:F2}) status: {order.CurrentStatus}."
        };

        // Notify every admin user in the system
        var adminUsers = _context.Users
            .Where(u => u.Role == "Admin" && u.IsActive)
            .Select(u => u.UserId)
            .ToList();

        foreach (var adminId in adminUsers)
        {
            _notificationService.CreateAsync(
                userId:  adminId,
                title:   title,
                message: message,
                type:    "OrderStatus",
                orderId: order.OrderId
            ).GetAwaiter().GetResult();
        }
    }
}