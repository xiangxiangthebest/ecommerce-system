using System.Security.Claims;
using EcommerceSystem.Models;

namespace EcommerceSystem.Interfaces
{
    public interface ICustomerServiceContext
    {
        Task<CustomerService?> GetCurrentCustomerServiceAsync(ClaimsPrincipal user);
    }
}