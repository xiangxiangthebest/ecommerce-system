using EcommerceSystem.Enums;
using EcommerceSystem.Interfaces;

namespace EcommerceSystem.Models;
public class ReturnRefundStrategy : IRequestStrategy
{
    public void Solve(Request request)
    {   
        if (request.Order == null) return;
        request.Order.CurrentStatus = OrderStatus.RETURN_REFUND;
    }
}