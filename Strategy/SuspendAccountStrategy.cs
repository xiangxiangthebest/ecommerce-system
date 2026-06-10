using EcommerceSystem.Enums;
using EcommerceSystem.Interfaces;
using EcommerceSystem.Models;
public class SuspendAccountStrategy : IRequestStrategy
{
    public void Solve(Request request)
    {   
        if (request.ReportedUser == null) return;
        request.ReportedUser.IsActive = false;
    }
}