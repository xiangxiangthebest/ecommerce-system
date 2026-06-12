namespace EcommerceSystem.Models;

public class Review
{
    public int ReviewId { get; set; }
    public int OrderItemId { get; set; }
    public OrderItem? OrderItem { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int CustomerId { get; set; }
    public int Rating { get; set; }   // 1–5
    public string ReviewText { get; set; } = "";

    public string? ReviewImagePathsJson { get; set; }

    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
}