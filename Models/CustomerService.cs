using EcommerceSystem.Interfaces;
namespace EcommerceSystem.Models;

public class CustomerService : User
{
    public void AssignStrategy(Request request, IRequestStrategy strategy)
    {
        request.SetRequestStrategy(strategy);
    }
}
