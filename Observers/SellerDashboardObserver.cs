using EcommerceSystem.Models;
using EcommerceSystem.Interfaces;

namespace EcommerceSystem.Observers;

public class SellerDashboardObserver : Observer
{
    private Order? _order; 

    public void Update(Order order)
    {
        _order = order;
        RefreshSellerOrder();
    }

    public void RefreshSellerOrder()
    {
        Console.WriteLine($"[Seller Dashboard] " +
                          $"Order #{_order?.OrderId} " +
                          $"from Shop: {_order?.Seller?.ShopName} " +
                          $"is now: {_order?.CurrentStatus}");
    }
}

