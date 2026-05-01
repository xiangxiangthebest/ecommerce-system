namespace EcommerceSystem.Models;

public class Payment
{
    public int PaymentId { get; set; }

    public int OrderId { get; set; }

    public string PaymentMethod { get; set; }

    public string PaymentStatus { get; set; }

    public DateTime PaidAt { get; set; }

    public Order? Order { get; set; }
}