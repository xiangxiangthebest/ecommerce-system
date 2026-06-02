using System.Security.Claims;
using EcommerceSystem.Models;

namespace EcommerceSystem.Interfaces
{
    public interface ISellerContext
    {
        Task<Seller?> GetCurrentSellerAsync(ClaimsPrincipal user);
    }
}