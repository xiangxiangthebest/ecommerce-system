using EcommerceSystem.DTOs;
using EcommerceSystem.Models;
using Microsoft.AspNetCore.Http;

namespace EcommerceSystem.Interfaces
{
    public interface IReviewReportService
    {
        Task<ReviewReport> CreateReviewReportAsync(int customerId, CreateReviewReportDto dto, List<IFormFile> evidenceFiles);
        Task<ReviewReport?> GetReportByIdAsync(int reportId);
        Task<List<ReviewReport>> GetReportsByReviewIdAsync(int reviewId);
        Task<List<ReviewReport>> GetReportsByCustomerIdAsync(int customerId);
        Task<List<ReviewReport>> GetAllReportsAsync();
        Task<bool> UpdateReportStatusAsync(int reportId, string newStatus);
        Task<bool> DeleteReportAsync(int reportId);
    }
}
