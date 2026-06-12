using System.Security.Claims;
using EcommerceSystem.Data;
using EcommerceSystem.Interfaces;
using EcommerceSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace EcommerceSystem.Services
{
    public class CustomerServiceContext : ICustomerServiceContext
    {
        private readonly AppDbContext _context;

        public CustomerServiceContext(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CustomerService?> GetCurrentCustomerServiceAsync(ClaimsPrincipal user)
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId)) return null;

            return await _context.Users
                .OfType<CustomerService>()
                .FirstOrDefaultAsync(x => x.UserId == int.Parse(userId));
        }
    }
}