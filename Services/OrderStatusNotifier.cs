using EcommerceSystem.Interfaces;
using EcommerceSystem.Models;

namespace EcommerceSystem.Observers;

public class OrderStatusNotifier : OrderStatusSubject
{
    private readonly List<OrderStatusObserver> _observers = new();
    private Order _order = null!;

    // Subject interface

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

    // Public trigger 
    public async Task SetOrderAndNotifyAsync(Order order)
    {
        _order = order;
        await NotifyObserversAsync();
    }
}
