

namespace EcommerceSystem.Interfaces;

public interface OrderStatusObserver
{
    Task Update(Order order);
}

