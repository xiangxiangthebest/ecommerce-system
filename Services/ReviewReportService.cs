using EcommerceSystem.Data;
using EcommerceSystem.DTOs;
using EcommerceSystem.Interfaces;
using EcommerceSystem.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EcommerceSystem.Services
{
    public class ReviewReportService : IReviewReportService
    {
        private readonly AppDbContext _context;
        private readonly IReportImageStorage _reportImageStorage;

        public ReviewReportService(AppDbContext context, IReportImageStorage reportImageStorage)
        {
            _context = context;
            _reportImageStorage = reportImageStorage;
        }

        public async Task<ReviewReport> CreateReviewReportAsync(int customerId, CreateReviewReportDto dto, List<IFormFile> evidenceFiles)
        {
            // Validate review exists
            var review = await _context.Reviews.FindAsync(dto.ReviewId);
            if (review == null)
                throw new InvalidOperationException($"Review with ID {dto.ReviewId} not found.");

            // Validate customer exists
            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null)
                throw new InvalidOperationException($"Customer with ID {customerId} not found.");

            // Validate customer is not reporting their own review
            if (review.CustomerId == customerId)
                throw new InvalidOperationException("You cannot report your own review.");

            // Save evidence images
            var evidencePaths = new List<string>();
            if (evidenceFiles != null && evidenceFiles.Count > 0)
            {
                evidencePaths = await _reportImageStorage.SaveReportEvidenceImagesAsync(evidenceFiles);
            }

            // Create report
            var report = new ReviewReport
            {
                ReviewId = dto.ReviewId,
                CustomerId = customerId,
                ReportReason = dto.ReportReason,
                ReportDescription = dto.ReportDescription,
                EvidenceImagePathsJson = JsonSerializer.Serialize(evidencePaths),
                Status = "Pending",
                ReportedAt = DateTime.UtcNow
            };

            _context.ReviewReports.Add(report);
            await _context.SaveChangesAsync();

            return report;
        }

        public async Task<ReviewReport?> GetReportByIdAsync(int reportId)
        {
        return await _context.ReviewReports
            .Include(r => r.Review!)
                .ThenInclude(rev => rev.OrderItem!)
                    .ThenInclude(oi => oi.Order!)
                        .ThenInclude(o => o.Customer!)
            .Include(r => r.Review!)
                .ThenInclude(rev => rev.Product!)
            .Include(r => r.Customer)
            .FirstOrDefaultAsync(r => r.ReviewReportId == reportId);
        }

        public async Task<List<ReviewReport>> GetReportsByReviewIdAsync(int reviewId)
        {
            return await _context.ReviewReports
                .Where(r => r.ReviewId == reviewId)
                .Include(r => r.Review!)
                    .ThenInclude(rev => rev.OrderItem!)
                        .ThenInclude(oi => oi.Order!)
                            .ThenInclude(o => o.Customer!)
                .Include(r => r.Review!)
                    .ThenInclude(rev => rev.Product!)
                .Include(r => r.Customer)
                .OrderByDescending(r => r.ReportedAt)
                .ToListAsync();
        }

        public async Task<List<ReviewReport>> GetReportsByCustomerIdAsync(int customerId)
        {
            return await _context.ReviewReports
                .Where(r => r.CustomerId == customerId)
                .Include(r => r.Review)
                    .ThenInclude(rev => rev.OrderItem)
                        .ThenInclude(oi => oi.Order)
                            .ThenInclude(o => o.Customer)
                .Include(r => r.Review)
                    .ThenInclude(rev => rev.Product)
                .Include(r => r.Customer)
                .OrderByDescending(r => r.ReportedAt)
                .ToListAsync();
        }

        public async Task<List<ReviewReport>> GetAllReportsAsync()
        {
            return await _context.ReviewReports
                .Include(r => r.Review)
                    .ThenInclude(rev => rev.OrderItem)
                        .ThenInclude(oi => oi.Order)
                            .ThenInclude(o => o.Customer)
                .Include(r => r.Review)
                    .ThenInclude(rev => rev.Product)
                .Include(r => r.Customer)
                .OrderByDescending(r => r.ReportedAt)
                .ToListAsync();
        }

        public async Task<bool> DeleteReportAsync(int reportId)
        {
            var report = await _context.ReviewReports.FindAsync(reportId);
            if (report == null)
                return false;

            _context.ReviewReports.Remove(report);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
