using EcommerceSystem.Models;
using EcommerceSystem.Interfaces;

namespace EcommerceSystem.Observers;

public class AdminPanelObserver : Observer
{
    private Order? _order; 
    public void Update(Order order)
    {
        _order = order;
        RefreshView();
    }

    public void RefreshView()
    {
        Console.WriteLine($"[Admin Panel] " +
                          $"Order #{_order?.OrderId} " +
                          $"Total: RM{_order?.TotalAmount} " +
                          $"is now: {_order?.CurrentStatus}");
    }
}

