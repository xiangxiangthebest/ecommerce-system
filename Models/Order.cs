namespace EcommerceSystem.Models;

public class Order
{
    public int OrderId { get; set; }

    public int CustomerId { get; set; }

    public User? Customer { get; set; }

    public DateTime OrderDate { get; set; }

    public decimal TotalAmount { get; set; }

    public string OrderStatus { get; set; } = "Pending";

    public ICollection<OrderItem>? OrderItems { get; set; }
}