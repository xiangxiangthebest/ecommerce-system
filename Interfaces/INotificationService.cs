using EcommerceSystem.Models;

namespace EcommerceSystem.Interfaces
{
    public interface INotificationService
    {
        Task CreateAsync(int userId, string title, string message, string type, int? orderId = null);
        Task<List<Notification>> GetUserNotificationsAsync(int userId);
        Task MarkAsReadAsync(int notificationId, int userId);
        Task<int> GetUnreadCountAsync(int userId);
    }
}