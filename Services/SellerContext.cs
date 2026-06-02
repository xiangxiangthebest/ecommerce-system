using System.Security.Claims;
using EcommerceSystem.Data;
using EcommerceSystem.Interfaces;
using EcommerceSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace EcommerceSystem.Services
{
    public class SellerContext : ISellerContext
    {
        private readonly AppDbContext _context;

        public SellerContext(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Seller?> GetCurrentSellerAsync(ClaimsPrincipal user)
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId)) return null;

            return await _context.Users
                .OfType<Seller>()
                .FirstOrDefaultAsync(x => x.UserId == int.Parse(userId));
        }
    }
}