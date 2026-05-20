using System.Security.Claims;
using EcommerceSystem.Models;

namespace EcommerceSystem.Interfaces
{
    public interface ICustomerContext
    {
        Task<Customer?> GetCurrentCustomerAsync(ClaimsPrincipal user);
    }
}