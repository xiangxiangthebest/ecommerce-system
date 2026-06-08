using EcommerceSystem.Data;
using EcommerceSystem.DTOs;
using EcommerceSystem.Interfaces;
using EcommerceSystem.Models;
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
                .Include(r => r.Customer!)
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
                .Include(r => r.Customer!)
                .OrderByDescending(r => r.ReportedAt)
                .ToListAsync();
        }

        public async Task<List<ReviewReport>> GetReportsByCustomerIdAsync(int customerId)
        {
            return await _context.ReviewReports
                .Where(r => r.CustomerId == customerId)
                .Include(r => r.Review!)
                    .ThenInclude(rev => rev.OrderItem!)
                        .ThenInclude(oi => oi.Order!)
                            .ThenInclude(o => o.Customer!)
                .Include(r => r.Review!)
                    .ThenInclude(rev => rev.Product!)
                .Include(r => r.Customer!)
                .OrderByDescending(r => r.ReportedAt)
                .ToListAsync();
        }

        public async Task<List<ReviewReport>> GetAllReportsAsync()
        {
            return await _context.ReviewReports
                .Include(r => r.Review!)
                    .ThenInclude(rev => rev.OrderItem!)
                        .ThenInclude(oi => oi.Order!)
                            .ThenInclude(o => o.Customer!)
                .Include(r => r.Review!)
                    .ThenInclude(rev => rev.Product!)
                .Include(r => r.Customer!)
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

        public async Task<bool> UpdateReportStatusAsync(int reportId, string newStatus)
        {
            var report = await _context.ReviewReports
                .Include(r => r.Review!)
                    .ThenInclude(rev => rev.Product!)
                .Include(r => r.Review!)
                    .ThenInclude(rev => rev.OrderItem!)
                        .ThenInclude(oi => oi.Order!)
                            .ThenInclude(o => o.Customer!)
                .FirstOrDefaultAsync(r => r.ReviewReportId == reportId);
            if (report == null)
                return false;

            // Validate status is one of the allowed values
            var validStatuses = new[] { "Pending", "Approved", "Rejected" };
            if (!validStatuses.Contains(newStatus))
                throw new ArgumentException($"Invalid status. Must be one of: {string.Join(", ", validStatuses)}");

            report.Status = newStatus;
            report.ResolvedAt = DateTime.UtcNow;

            // If approved, delete the review but keep the report record with snapshot
            if (newStatus == "Approved" && report.Review != null)
            {
                var product = report.Review.Product;
                var review = report.Review;
                var deletedRating = review.Rating;

                // Save snapshot of review data before deletion
                report.SavedProductName = product?.Name ?? "Unknown Product";
                report.SavedRating = review.Rating;
                report.SavedReviewText = review.ReviewText;
                report.SavedReviewerName = review.OrderItem?.Order?.Customer?.FullName ?? "Customer";

                // Nullify the foreign key FIRST to prevent cascade delete
                report.ReviewId = null;

                // Now delete the review
                _context.Reviews.Remove(review);

                // Recalculate product's average rating and review count
                if (product != null)
                {
                    int newCount = (int)product.ReviewCount - 1;
                    
                    if (newCount <= 0)
                    {
                        // No reviews left
                        product.AverageRating = 0;
                        product.ReviewCount = 0;
                    }
                    else
                    {
                        // Recalculate average: (old_sum - deleted_rating) / new_count
                        double oldSum = product.AverageRating * (int)product.ReviewCount;
                        double newSum = oldSum - deletedRating;
                        product.AverageRating = newSum / newCount;
                        product.ReviewCount = newCount;
                    }
                }
            }

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
