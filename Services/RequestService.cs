using EcommerceSystem.Data;
using EcommerceSystem.Interfaces;
using EcommerceSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace EcommerceSystem.Services
{
    public class RequestService : IRequestService
    {
        private readonly AppDbContext _context;

        public RequestService(AppDbContext context)
        {
            _context = context;
        }

        // Get all requests created by user
        public async Task<List<Request>> GetByUserId(int userId)
        {
            return await _context.Request
                .Include(r => r.Customer)
                .Include(r => r.Order)
                .Include(r => r.Images)
                .Where(r => r.CustomerId == userId)
                .OrderByDescending(r => r.RequestId)
                .ToListAsync();
        }

        // Get requests related to an order
        public async Task<List<Request>> GetByOrderId(int orderId)
        {
            return await _context.Request
                .Include(r => r.Customer)
                .Include(r => r.Images)
                .Where(r => r.OrderId == orderId)
                .OrderByDescending(r => r.RequestId)
                .ToListAsync();
        }

        // Get single request
        public async Task<Request?> GetById(int requestId)
        {
            return await _context.Request
                .Include(r => r.Customer)
                .Include(r => r.Order)
                .Include(r => r.Images)
                .FirstOrDefaultAsync(r => r.RequestId == requestId);
        }

        // Create request
        public async Task CreateAsync(Request request)
        {
            _context.Request.Add(request);
            await _context.SaveChangesAsync();
        }

        public async Task<List<RequestImage>> GetRequestImagesByRequestId(int requestId)
        {
            return await _context.RequestImages
                .Where(x => x.RequestId == requestId)
                .ToListAsync();
        }
    }
}