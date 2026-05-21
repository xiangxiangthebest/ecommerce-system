using System.Security.Claims;
using EcommerceSystem.Data;
using EcommerceSystem.Interfaces;
using EcommerceSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace EcommerceSystem.Services
{
    public class CustomerContext : ICustomerContext
    {
        private readonly AppDbContext _context;

        public CustomerContext(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Customer?> GetCurrentCustomerAsync(ClaimsPrincipal user)
        {
            var email = user.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrWhiteSpace(email)) return null;

            return await _context.Users
                .OfType<Customer>()
                .FirstOrDefaultAsync(x => x.Email == email);
        }
    }
}