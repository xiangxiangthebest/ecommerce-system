using EcommerceSystem.Enums;
using EcommerceSystem.Models;
using EcommerceSystem.Interfaces;

namespace EcommerceSystem.Models;
public class ActivateAccountStrategy : IRequestStrategy
{
    public void Solve(Request request)
    {
        if (request.ReportedUser == null) return;
        request.ReportedUser.IsActive = true;
    }
}