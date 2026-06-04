namespace EcommerceSystem.StrategyPattern;
public class SuspendAccountStrategy : IRequestStrategy
{
    public void Solve(Request request)
    {
        request.Result = "Account suspended";
        request.ReportedUser.IsActive = false;
    }
}