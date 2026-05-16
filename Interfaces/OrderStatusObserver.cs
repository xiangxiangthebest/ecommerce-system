using EcommerceSystem.Models; 

namespace EcommerceSystem.Interfaces;

public interface OrderStatusObserver
{
    void Update(Order order);
}

