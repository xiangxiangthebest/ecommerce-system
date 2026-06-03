namespace EcommerceSystem.Interfaces;

public interface OrderStatusSubject
{
    void Attach(OrderStatusObserver observer);
    void Detach(OrderStatusObserver observer);
    Task NotifyObserversAsync();
}

