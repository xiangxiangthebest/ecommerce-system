using EcommerceSystem.Models;
using EcommerceSystem.Interfaces;

namespace EcommerceSystem.Observers;

public class CustomerDashboardObserver : Observer
{
    private Order? _order; 

    public void Update(Order order)
    {
        _order = order;
        RefreshOrderStatus();
    }

    public void RefreshOrderStatus()
    {
        Console.WriteLine($"[Customer Dashboard] " +
                          $"Order #{_order?.OrderId} " +
                          $"is now: {_order?.CurrentStatus}");
    }
}

