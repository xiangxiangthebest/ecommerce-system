namespace EcommerceSystem.StrategyPattern;
using EcommerceSystem.Models;
public class RefundStrategy : IRequestStrategy
{
    public void Solve(Request request)
    {
        request.Result = "Refund processed";
        request.Order.CurrentStatus = OrderStatus.RETURN;
    }
}