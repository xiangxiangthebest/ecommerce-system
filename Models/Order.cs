//namespace EcommerceSystem.Models;

//public class Order
//{
    //public int OrderId { get; set; }
    // public Customer customer { get; set; }
    // public Seller seller { get; set; }
    // public DateTime orderTime { get; set; }
    // public List<CartProduct> products { get; set; }
    // public decimal totalAmount { get; set; }

    // public string paymentMethod { get; set; }

    // public Order(int orderId, Customer customer, Seller seller, DateTime orderTime, List<CartProduct> products, decimal totalAmount, string paymentMethod)
    // {
    //     this.orderId = orderId;
    //     this.customer = customer;
    //     this.seller = seller;
    //     this.orderTime = orderTime;
    //     this.products = products ?? new List<CartProduct>();
    //     this.totalAmount = totalAmount;
    //     this.paymentMethod = paymentMethod;

    // }

    // public OrderStatus GetOrderStatus(){
    // // Will return the lowest priority status:
    // // Delivered > Shipped > Pending
    // // Return Canceled if all items are canceled

    // OrderStatus status = OrderStatus.Delivered;
    // int cancelled = 0;

    // foreach (CartProduct p in products)
    // {
    //     // If at least one item is shipped,
    //     // and no pending item exists yet
    //     if (p.GetItemStatus() == OrderStatus.Shipped &&
    //         status != OrderStatus.Pending)
    //     {
    //         status = OrderStatus.Shipped;
    //     }

    //     // If any item is pending, highest priority
    //     if (p.GetItemStatus() == OrderStatus.Pending)
    //     {
    //         status = OrderStatus.Pending;
    //     }

    //     if (p.GetItemStatus() == OrderStatus.Canceled)
    //     {
    //         cancelled++;
    //     }
    // }

    // if (cancelled == products.Count)
    // {
    //     return OrderStatus.Canceled;
    // }
    // else
    // {
    //     return status;
    // }
// }
//}

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

    // Navigation properties
    public Customer? Customer { get; set; }
    public Seller? Seller { get; set; }
    public List<CartProduct> Products { get; set; } = new();

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


