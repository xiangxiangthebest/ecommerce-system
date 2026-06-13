using EcommerceSystem.Interfaces;
namespace EcommerceSystem.Models;

public class CustomerService : User
{
    // public string Phone { get; set; } = string.Empty;
    public int TicketsResolved { get; set; }
    // public DateTime HireDate { get; set; }

    public void AssignStrategy(Request request, IRequestStrategy strategy)
    {
        request.SetRequestStrategy(strategy);
    }
}
