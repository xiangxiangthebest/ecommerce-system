using System.ComponentModel.DataAnnotations.Schema;
using EcommerceSystem.Interfaces;
using EcommerceSystem.Enums;

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
    public decimal ApprovedRefundAmount { get; set; } = 0m;  
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

    // ── Cancellation ────────────────────────────────────────────────────────
    public string? CancelReason { get; set; }
    public DateTime? CanceledAt { get; set; }

    // ── Delivery / Receipt ──────────────────────────────────────────────────
    /// <summary>Set when status becomes DELIVERED — used by AutoReceiveOrdersJob for 3-day countdown.</summary>
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ReceivedAt { get; set; }

    // ── Return / Refund ─────────────────────────────────────────────────────
    public string? ReturnReason { get; set; }

    /// <summary>The kind of resolution the customer is requesting (ReturnRefund / ReturnReplace / RefundOnly).</summary>
    public ReturnType? ReturnType { get; set; }

    /// <summary>JSON-serialised list of image paths uploaded by the customer with the return request.</summary>
    public string? ReturnImagePathsJson { get; set; }

    public DateTime? ReturnInitiatedAt { get; set; }
    public ReturnInitiatedBy? ReturnInitiatedBy { get; set; }
    public ReturnStatus ReturnStatus { get; set; } = ReturnStatus.None;
    public bool ReturnRequested { get; set; } = false;

    /// <summary>Set when seller approves the return/refund request — triggers stock restoration.</summary>
    public DateTime? ReturnApprovedAt { get; set; }

    // ── Review flag (not persisted) ─────────────────────────────────────────
    [NotMapped]
    public bool ReviewSubmitted { get; set; }

    // ── Navigation properties ────────────────────────────────────────────────
    public Customer? Customer { get; set; }
    public Seller? Seller { get; set; }

    // ── Observer list (not persisted) ───────────────────────────────────────
    [NotMapped]
    private List<OrderStatusObserver> _observers = new List<OrderStatusObserver>();

    public void Attach(OrderStatusObserver observer) => _observers.Add(observer);
    public void Detach(OrderStatusObserver observer) => _observers.Remove(observer);

    public async Task NotifyObserversAsync()
    {
        foreach (var o in _observers)
            await o.Update(this);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SELLER TRANSITIONS  (shown in the seller Order page dropdown)
    //
    //   PENDING   → PREPARING   Seller accepts and starts preparing the order.
    //   PREPARING → SHIPPED     Seller hands the parcel to the courier.
    //   SHIPPED   → DELIVERED   Courier delivers to the customer's address.
    //
    // ❌ CANCELED      — customer-only. Seller cannot cancel.
    // ❌ RECEIVED      — triggered by customer or auto-job.
    // ❌ RETURN_REFUND — initiated by customer only.
    // ─────────────────────────────────────────────────────────────────────────
    public static readonly Dictionary<OrderStatus, List<OrderStatus>> SellerAllowedTransitions = new()
    {
        { OrderStatus.PENDING,       new() { OrderStatus.PREPARING } },
        { OrderStatus.PREPARING,     new() { OrderStatus.SHIPPED } },
        { OrderStatus.SHIPPED,       new() { OrderStatus.DELIVERED } },
        { OrderStatus.DELIVERED,     new() { } },
        { OrderStatus.RECEIVED,      new() { } },
        { OrderStatus.RETURN_REFUND, new() { } },
        { OrderStatus.CANCELED,      new() { } },
    };

    // ─────────────────────────────────────────────────────────────────────────
    // CUSTOMER TRANSITIONS
    //
    //   PENDING   → CANCELED      Customer cancels before seller starts.
    //   DELIVERED → RECEIVED      Customer clicks "I received my parcel".
    //   DELIVERED → RETURN_REFUND Customer raises an issue on delivery.
    //   RECEIVED  → RETURN_REFUND Customer raises issue after confirming receipt.
    // ─────────────────────────────────────────────────────────────────────────
    public static readonly Dictionary<OrderStatus, List<OrderStatus>> CustomerAllowedTransitions = new()
    {
        { OrderStatus.PENDING,       new() { OrderStatus.CANCELED } },
        { OrderStatus.PREPARING,     new() { } },
        { OrderStatus.SHIPPED,       new() { } },
        { OrderStatus.DELIVERED,     new() { OrderStatus.RECEIVED, OrderStatus.RETURN_REFUND } },
        { OrderStatus.RECEIVED,      new() { OrderStatus.RETURN_REFUND } },
        { OrderStatus.RETURN_REFUND, new() { } },
        { OrderStatus.CANCELED,      new() { } },
    };

    // ─────────────────────────────────────────────────────────────────────────
    // FULL TRANSITION MAP  (union of all actors + background job)
    // Used internally by CanTransitionTo() — role enforcement is separate.
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
    /// Use SellerAllowedTransitions / CustomerAllowedTransitions for role-level enforcement.
    /// </summary>
    public bool CanTransitionTo(OrderStatus next)
    {
        return AllAllowedTransitions.TryGetValue(CurrentStatus, out var allowed)
               && allowed.Contains(next);
    }

    /// <summary>
    /// Validates the transition, updates the status, stamps timestamps,
    /// then notifies all attached observers asynchronously.
    /// Throws InvalidOperationException if the transition is not allowed.
    /// </summary>
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
            case OrderStatus.RETURN_REFUND:
                ReturnInitiatedAt = DateTime.UtcNow;
                break;
        }

        await NotifyObserversAsync();
    }
}