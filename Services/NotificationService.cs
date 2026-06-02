using EcommerceSystem.Data;
using EcommerceSystem.Interfaces;
using EcommerceSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace EcommerceSystem.Services;

public class NotificationService : INotificationService
{
    private readonly AppDbContext _context;

    public NotificationService(AppDbContext context)
    {
        _context = context;
    }

    public async Task CreateAsync(int userId, string title, string message,
                                  string type = "OrderStatus", int? orderId = null)
    {
        var notification = new Notification
        {
            UserId    = userId,
            Title     = title,
            Message   = message,
            Type      = type,
            OrderId   = orderId,
            IsRead    = false,
            CreatedAt = DateTime.UtcNow
        };
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();
    }

    public async Task<List<Notification>> GetForUserAsync(int userId) =>
        await _context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

    public async Task MarkAsReadAsync(int notificationId)
    {
        var n = await _context.Notifications.FindAsync(notificationId);
        if (n != null) { n.IsRead = true; await _context.SaveChangesAsync(); }
    }

    public async Task MarkAllAsReadAsync(int userId)
    {
        var unread = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();
        unread.ForEach(n => n.IsRead = true);
        await _context.SaveChangesAsync();
    }

    public async Task<int> GetUnreadCountAsync(int userId) =>
        await _context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead);
}