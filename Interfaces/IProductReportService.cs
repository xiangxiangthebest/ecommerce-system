using EcommerceSystem.DTOs;
using EcommerceSystem.Models;
using Microsoft.AspNetCore.Http;

namespace EcommerceSystem.Interfaces
{
    public interface IProductReportService
    {
        Task<ProductReport> CreateProductReportAsync(int customerId, CreateProductReportDto dto, List<IFormFile> evidenceFiles);
        Task<ProductReport?> GetReportByIdAsync(int reportId);
        Task<List<ProductReport>> GetReportsByProductIdAsync(int productId);
        Task<List<ProductReport>> GetReportsByCustomerIdAsync(int customerId);
        Task<List<ProductReport>> GetAllReportsAsync();
        Task<bool> UpdateReportStatusAsync(int reportId, string newStatus);
        Task<bool> DeleteReportAsync(int reportId);
    }
}
