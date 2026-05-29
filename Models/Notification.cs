namespace EcommerceSystem.Models;

public class Notification
{
    public int NotificationId { get; set; }
    public int UserId         { get; set; }
    public string Title       { get; set; } = string.Empty;
    public string Message     { get; set; } = string.Empty;
    public string Type        { get; set; } = "OrderStatus";
    public int?   OrderId     { get; set; }
    public bool   IsRead      { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User? User { get; set; }
}