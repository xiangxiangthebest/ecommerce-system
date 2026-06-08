using EcommerceSystem.Models;

namespace EcommerceSystem.Interfaces
{
    public interface IRequestService
    {
        Task<List<Request>> GetByUserId(int userId);
        Task<List<Request>> GetByOrderId(int orderId);
        Task<Request?> GetById(int requestId);
        Task CreateAsync(Request request);
    }
}