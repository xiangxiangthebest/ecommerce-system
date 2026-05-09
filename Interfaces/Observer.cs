using EcommerceSystem.Models; 

namespace EcommerceSystem.Interfaces;

public interface Observer
{
    void Update(Order order);
}

