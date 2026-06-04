namespace EcommerceSystem.Models;

public class CustomerService : User
{
    public string Phone { get; set; } = string.Empty;
    public int TicketsResolved { get; set; }
    public DateTime HireDate { get; set; }
}
