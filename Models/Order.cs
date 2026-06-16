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
    public bool VoucherApplied { get; set; } = false;
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

    public string? CancelReason { get; set; }
    public DateTime? CanceledAt { get; set; }

    public DateTime? DeliveredAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public bool ReviewSubmitted { get; set; } = false;
    public Customer? Customer { get; set; }
    public Seller? Seller { get; set; }

    [NotMapped]
    private List<OrderStatusObserver> _observers = new List<OrderStatusObserver>();

    public void Attach(OrderStatusObserver observer) => _observers.Add(observer);
    public void Detach(OrderStatusObserver observer) => _observers.Remove(observer);

    public async Task NotifyObserversAsync()
    {
        foreach (var o in _observers)
            await o.Update(this);
    }

    public static readonly Dictionary<OrderStatus, List<OrderStatus>> SellerAllowedTransitions = new()
    {
        { OrderStatus.PENDING,       new() { OrderStatus.PREPARING } },
        { OrderStatus.PREPARING,     new() { OrderStatus.SHIPPED } },
        { OrderStatus.SHIPPED,       new() { OrderStatus.DELIVERED } },
        { OrderStatus.DELIVERED,     new() { } },
        { OrderStatus.RECEIVED,      new() { } },
        { OrderStatus.CANCELED,      new() { } },
    };

    public static readonly Dictionary<OrderStatus, List<OrderStatus>> CustomerAllowedTransitions = new()
    {
        { OrderStatus.PENDING,       new() { OrderStatus.CANCELED } },
        { OrderStatus.PREPARING,     new() { OrderStatus.CANCEL_REQUESTED } },
        { OrderStatus.SHIPPED,       new() { } },
        { OrderStatus.DELIVERED,     new() { OrderStatus.RECEIVED, OrderStatus.RETURN_REFUND_REQUESTED } },
        { OrderStatus.RECEIVED,      new() { OrderStatus.RETURN_REFUND_REQUESTED } },

        { OrderStatus.RETURN_REFUND_REQUESTED, new() { } },

        { OrderStatus.CANCELED, new() { } },
        { OrderStatus.CANCEL_REQUESTED, new() { } }

    };

    private static readonly Dictionary<OrderStatus, List<OrderStatus>> AllAllowedTransitions = new()
    {
        { OrderStatus.PENDING,       new() { OrderStatus.PREPARING, OrderStatus.CANCELED } },
        { OrderStatus.PREPARING,     new() { OrderStatus.SHIPPED, OrderStatus.CANCEL_REQUESTED } },
        { OrderStatus.SHIPPED,       new() { OrderStatus.DELIVERED } },
        { OrderStatus.DELIVERED,     new() { OrderStatus.RECEIVED, OrderStatus.RETURN_REFUND_REQUESTED } },
        { OrderStatus.RECEIVED,      new() { OrderStatus.RETURN_REFUND_REQUESTED } },
        { OrderStatus.CANCELED,      new() { } },

        { OrderStatus.CANCEL_REQUESTED, new() { OrderStatus.CANCELED} },
        { OrderStatus.RETURN_REFUND_REQUESTED, new(){ }}
    };

    public bool CanTransitionTo(OrderStatus next)
    {
        return AllAllowedTransitions.TryGetValue(CurrentStatus, out var allowed)
        && allowed.Contains(next);
    }

    public async Task SetStatusAsync(OrderStatus newStatus)
    {
        if (!CanTransitionTo(newStatus))
            throw new InvalidOperationException(
                $"Cannot transition order from {CurrentStatus} to {newStatus}.");

        CurrentStatus = newStatus;

        switch (newStatus)
        {
            case OrderStatus.DELIVERED:
                DeliveredAt = DateTime.UtcNow;
                break;
            case OrderStatus.RECEIVED:
                ReceivedAt = DateTime.UtcNow;
                break;
            case OrderStatus.CANCELED:
                CanceledAt = DateTime.UtcNow;
                break;
        }

        await NotifyObserversAsync();
    }
}