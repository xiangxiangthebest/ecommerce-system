using System.ComponentModel.DataAnnotations.Schema;
using EcommerceSystem.Interfaces;

namespace EcommerceSystem.Models;

public class Order : OrderStatusSubject
{
    public int OrderId { get; set; }
    public int CustomerUserId { get; set; }
    public int SellerUserId { get; set; }
    public OrderStatus CurrentStatus { get; set; } = OrderStatus.PENDING;
    public DateTime OrderTime { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public int? AddressId { get; set; }
    public string? CustomerMessage { get; set; }
    public List<OrderItem> OrderItems { get; set; } = new();
    public DeliveryField? Address { get; set; }
    public string DeliveryRecipientName { get; set; } = string.Empty;
    public string DeliveryPhoneNumber { get; set; } = string.Empty;
    public string DeliveryAddressLine1 { get; set; } = string.Empty;
    public string? DeliveryAddressLine2 { get; set; }
    public string DeliveryCity { get; set; } = string.Empty;
    public string DeliveryPostcode { get; set; } = string.Empty;
    public string DeliveryState { get; set; } = string.Empty;

    // Navigation properties
    public Customer? Customer { get; set; }
    public Seller? Seller { get; set; }
    // public List<CartProduct> Products { get; set; } = new();

    [NotMapped]
    private List<OrderStatusObserver> _observers = new List<OrderStatusObserver>();

    public void Attach(OrderStatusObserver observer) => _observers.Add(observer);
    public void Detach(OrderStatusObserver observer) => _observers.Remove(observer);

    public void NotifyObservers()
    {
        foreach (var o in _observers)
            o.Update(this);
    }

    public void SetStatus(OrderStatus status)
    {
        CurrentStatus = status;
        NotifyObservers();
    }
}


