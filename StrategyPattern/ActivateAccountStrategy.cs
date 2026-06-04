namespace EcommerceSystem.StrategyPattern;
public class ActivateAccountStrategy : IRequestStrategy
{
    public void Solve(Request request)
    {
        request.Result = "Account activated";
        request.ReportedUser.IsActive = true;
    }
}