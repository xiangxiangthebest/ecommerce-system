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
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId)) return null;

            return await _context.Users
                .OfType<Customer>()
                .FirstOrDefaultAsync(x => x.UserId == int.Parse(userId));
        }
    }
}