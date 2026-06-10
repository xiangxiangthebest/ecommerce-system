using EcommerceSystem.Interfaces;
using EcommerceSystem.Enums;
using EcommerceSystem.Models;
namespace EcommerceSystem.Models;

public class RefundStrategy : IRequestStrategy
{
    public void Solve(Request request)
    {
        if (request.Order == null) return;
        request.Order.CurrentStatus = OrderStatus.REFUND;
    }
}