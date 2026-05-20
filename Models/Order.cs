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
    public string? CancelReason { get; set; }
    public DateTime? CanceledAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public string? ReturnReason { get; set; }
    public ReturnType? ReturnType { get; set; }
    public DateTime? ReturnInitiatedAt { get; set; }
    public string? ComplaintText { get; set; }
    public bool ComplaintSubmitted { get; set; } = false;
    public DateTime? ComplaintAt { get; set; }

    // Set when status becomes DELIVERED — used by AutoReceiveOrdersJob for 3-day countdown
    public DateTime? DeliveredAt { get; set; }

    // Set when seller approves the return/refund request — triggers stock restoration
    public DateTime? ReturnApprovedAt { get; set; }

    [NotMapped]
    public bool ReviewSubmitted { get; set; }

    // Navigation properties
    public Customer? Customer { get; set; }
    public Seller? Seller { get; set; }

    // Observer list (not persisted)
    [NotMapped]
    private List<OrderStatusObserver> _observers = new List<OrderStatusObserver>();

    public void Attach(OrderStatusObserver observer) => _observers.Add(observer);
    public void Detach(OrderStatusObserver observer) => _observers.Remove(observer);

    public void NotifyObservers()
    {
        foreach (var o in _observers)
            o.Update(this);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SELLER TRANSITIONS  (shown in the seller Order page dropdown)
    //
    //   PENDING   → PREPARING
    //              Seller accepts and starts preparing the order.
    //
    //   PREPARING → SHIPPED
    //              Seller hands the parcel to the courier.
    //
    //   SHIPPED   → DELIVERED
    //              Courier has delivered the parcel to the customer's address.
    //
    // ❌ CANCELED      — customer-only. Seller cannot cancel.
    // ❌ RECEIVED      — triggered by customer clicking "Received" or auto-job.
    // ❌ RETURN_REFUND — initiated by customer only.
    // ─────────────────────────────────────────────────────────────────────────
    public static readonly Dictionary<OrderStatus, List<OrderStatus>> SellerAllowedTransitions = new()
    {
        { OrderStatus.PENDING,       new() { OrderStatus.PREPARING } },
        { OrderStatus.PREPARING,     new() { OrderStatus.SHIPPED } },
        { OrderStatus.SHIPPED,       new() { OrderStatus.DELIVERED } },
        { OrderStatus.DELIVERED,     new() { } },   // waiting for customer / auto-job
        { OrderStatus.RECEIVED,      new() { } },   // terminal for seller
        { OrderStatus.RETURN_REFUND, new() { } },   // terminal for seller
        { OrderStatus.CANCELED,      new() { } },   // terminal for seller
    };

    // ─────────────────────────────────────────────────────────────────────────
    // CUSTOMER TRANSITIONS  (used in CustomerController)
    //
    //   PENDING   → CANCELED      Customer cancels before seller starts.
    //   DELIVERED → RECEIVED      Customer clicks "I received my parcel".
    //   DELIVERED → RETURN_REFUND Customer raises an issue on delivery.
    //   RECEIVED  → RETURN_REFUND Customer raises issue after confirming receipt.
    //
    // ❌ Cannot cancel once PREPARING or later.
    // ─────────────────────────────────────────────────────────────────────────
    public static readonly Dictionary<OrderStatus, List<OrderStatus>> CustomerAllowedTransitions = new()
    {
        { OrderStatus.PENDING,       new() { OrderStatus.CANCELED } },
        { OrderStatus.PREPARING,     new() { } },   // too late to cancel
        { OrderStatus.SHIPPED,       new() { } },   // too late to cancel
        { OrderStatus.DELIVERED,     new() { OrderStatus.RECEIVED, OrderStatus.RETURN_REFUND } },
        { OrderStatus.RECEIVED,      new() { OrderStatus.RETURN_REFUND } },
        { OrderStatus.RETURN_REFUND, new() { } },   // terminal
        { OrderStatus.CANCELED,      new() { } },   // terminal
    };

    // ─────────────────────────────────────────────────────────────────────────
    // FULL TRANSITION MAP  (union of all actors + background job)
    // Used internally by CanTransitionTo() — validates that a transition is
    // physically possible, regardless of who triggered it.
    // Role-based restrictions are enforced separately by the controller.
    // ─────────────────────────────────────────────────────────────────────────
    private static readonly Dictionary<OrderStatus, List<OrderStatus>> AllAllowedTransitions = new()
    {
        { OrderStatus.PENDING,       new() { OrderStatus.PREPARING, OrderStatus.CANCELED } },
        { OrderStatus.PREPARING,     new() { OrderStatus.SHIPPED } },
        { OrderStatus.SHIPPED,       new() { OrderStatus.DELIVERED } },
        { OrderStatus.DELIVERED,     new() { OrderStatus.RECEIVED, OrderStatus.RETURN_REFUND } },
        { OrderStatus.RECEIVED,      new() { OrderStatus.RETURN_REFUND } },
        { OrderStatus.RETURN_REFUND, new() { } },
        { OrderStatus.CANCELED,      new() { } },
    };

    /// <summary>
    /// Returns true if transitioning to <paramref name="next"/> is physically
    /// possible from the current status. Does NOT check who is requesting.
    /// Use SellerAllowedTransitions / CustomerAllowedTransitions for UI and
    /// role-level enforcement in controllers.
    /// </summary>
    public bool CanTransitionTo(OrderStatus next)
    {
        return AllAllowedTransitions.TryGetValue(CurrentStatus, out var allowed)
               && allowed.Contains(next);
    }

    /// <summary>
    /// Validates the transition, updates the status, stamps timestamps,
    /// then notifies all attached observers.
    /// Throws InvalidOperationException if the transition is not allowed.
    /// </summary>
    public void SetStatus(OrderStatus newStatus)
    {
        if (!CanTransitionTo(newStatus))
            throw new InvalidOperationException(
                $"Cannot transition order from {CurrentStatus} to {newStatus}.");

        CurrentStatus = newStatus;

        // Auto-stamp the relevant timestamp
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
            case OrderStatus.RETURN_REFUND:
                ReturnInitiatedAt = DateTime.UtcNow;
                break;
        }

        NotifyObservers();
    }
}