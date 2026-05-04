namespace EcommerceSystem.Models;

public class Product
{
    public int Id { get; set; }
    public string SKU { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public string Category { get; set; }
    public string Description { get; set; }
    public string ImagePath { get; set; }
    public int SellerId { get; set; } // Link to the user who owns it
}