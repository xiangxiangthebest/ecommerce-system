namespace EcommerceSystem.Models;

public class Product
{
    public int ProductId { get; set; }

    public int SellerId { get; set; }

    public int CategoryId { get; set; }

    public string ProductName { get; set; }

    public string Description { get; set; }

    public decimal Price { get; set; }

    public int StockQuantity { get; set; }

    public string? ProductImage { get; set; }

    public string Status { get; set; } = "Pending";

    public User? Seller { get; set; }

    public Category? Category { get; set; }
}