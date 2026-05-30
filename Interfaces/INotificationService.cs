namespace EcommerceSystem.Interfaces;

public interface INotificationService
{
    Task CreateAsync(int userId, string title, string message,
                     string type = "OrderStatus", int? orderId = null);

    Task<List<EcommerceSystem.Models.Notification>> GetForUserAsync(int userId);
    Task MarkAsReadAsync(int notificationId);
    Task MarkAllAsReadAsync(int userId);
    Task<int> GetUnreadCountAsync(int userId);
}