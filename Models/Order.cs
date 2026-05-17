using System.ComponentModel.DataAnnotations.Schema;
using EcommerceSystem.Interfaces;

namespace EcommerceSystem.Models;

public class Order : Subject
{
    public int OrderId { get; set; }
    public int CustomerUserId { get; set; }
    public int SellerUserId { get; set; }
    public OrderStatus CurrentStatus { get; set; } = OrderStatus.PENDING;
    public DateTime OrderTime { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public int AddressId { get; set; }
    public List<OrderItem> OrderItems { get; set; } = new();
    public DeliveryField? Address { get; set; }

    // Navigation properties
    public Customer? Customer { get; set; }
    public Seller? Seller { get; set; }
    // public List<CartProduct> Products { get; set; } = new();

    [NotMapped]
    private List<Observer> _observers = new List<Observer>();

    public void Attach(Observer observer) => _observers.Add(observer);
    public void Detach(Observer observer) => _observers.Remove(observer);

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


