using EcommerceSystem.Interfaces;
using EcommerceSystem.Models;

namespace EcommerceSystem.Observers;

/// <summary>
/// Concrete Subject in the Observer pattern.
/// Holds the current Order and notifies all registered observers
/// whenever the order status changes.
/// </summary>
public class OrderStatusNotifier : OrderStatusSubject
{
    private readonly List<OrderStatusObserver> _observers = new();
    private Order _order = null!;

    // ── Subject interface ────────────────────────────────────────────────────

    public void Attach(OrderStatusObserver observer)
    {
        if (!_observers.Contains(observer))
            _observers.Add(observer);
    }

    public void Detach(OrderStatusObserver observer)
    {
        _observers.Remove(observer);
    }

    public async Task NotifyObserversAsync()
    {
        foreach (var observer in _observers)
            await observer.Update(_order);
    }

    // ── Public trigger ───────────────────────────────────────────────────────

    /// <summary>
    /// Call this after the order's CurrentStatus has been persisted.
    /// It stores the order and fans out to every attached observer.
    /// </summary>
    public async Task SetOrderAndNotifyAsync(Order order)
    {
        _order = order;
        await NotifyObserversAsync();
    }
}
