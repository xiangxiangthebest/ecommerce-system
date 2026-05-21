using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceSystem.Models;

public class OrderItem
{
    public int OrderItemId { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public string? SelectedVariation { get; set; }

    /// <summary>
    /// Populated in-memory by OrderService.GetPurchaseHistoryAsync — not persisted.
    /// Lets the purchase history view show the review alongside each order item.
    /// </summary>
    [NotMapped]
    public Review? Review { get; set; }
}