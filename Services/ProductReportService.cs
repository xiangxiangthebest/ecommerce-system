using EcommerceSystem.Data;
using EcommerceSystem.DTOs;
using EcommerceSystem.Interfaces;
using EcommerceSystem.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EcommerceSystem.Services
{
    public class ProductReportService : IProductReportService
    {
        private readonly AppDbContext _context;
        private readonly IReportImageStorage _reportImageStorage;

        public ProductReportService(AppDbContext context, IReportImageStorage reportImageStorage)
        {
            _context = context;
            _reportImageStorage = reportImageStorage;
        }

        public async Task<ProductReport> CreateProductReportAsync(int customerId, CreateProductReportDto dto, List<IFormFile> evidenceFiles)
        {
            // Validate product exists
            var product = await _context.Products.FindAsync(dto.ProductId);
            if (product == null)
                throw new InvalidOperationException($"Product with ID {dto.ProductId} not found.");

            // Validate customer exists
            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null)
                throw new InvalidOperationException($"Customer with ID {customerId} not found.");

            // Save evidence images
            var evidencePaths = new List<string>();
            if (evidenceFiles != null && evidenceFiles.Count > 0)
            {
                evidencePaths = await _reportImageStorage.SaveReportEvidenceImagesAsync(evidenceFiles);
            }

            // Create report
            var report = new ProductReport
            {
                ProductId = dto.ProductId,
                CustomerId = customerId,
                ReportReason = dto.ReportReason,
                ReportDescription = dto.ReportDescription,
                EvidenceImagePathsJson = JsonSerializer.Serialize(evidencePaths),
                Status = "Pending",
                ReportedAt = DateTime.UtcNow
            };

            _context.ProductReports.Add(report);
            await _context.SaveChangesAsync();

            return report;
        }

        public async Task<ProductReport?> GetReportByIdAsync(int reportId)
        {
            return await _context.ProductReports
                .Include(r => r.Product)
                .Include(r => r.Customer)
                .FirstOrDefaultAsync(r => r.ReportId == reportId);
        }

        public async Task<List<ProductReport>> GetReportsByProductIdAsync(int productId)
        {
            return await _context.ProductReports
                .Where(r => r.ProductId == productId)
                .Include(r => r.Product)
                .Include(r => r.Customer)
                .OrderByDescending(r => r.ReportedAt)
                .ToListAsync();
        }

        public async Task<List<ProductReport>> GetReportsByCustomerIdAsync(int customerId)
        {
            return await _context.ProductReports
                .Where(r => r.CustomerId == customerId)
                .Include(r => r.Product)
                .Include(r => r.Customer)
                .OrderByDescending(r => r.ReportedAt)
                .ToListAsync();
        }

        public async Task<List<ProductReport>> GetAllReportsAsync()
        {
            return await _context.ProductReports
                .Include(r => r.Product)
                .Include(r => r.Customer)
                .OrderByDescending(r => r.ReportedAt)
                .ToListAsync();
        }

        public async Task<bool> DeleteReportAsync(int reportId)
        {
            var report = await _context.ProductReports.FindAsync(reportId);
            if (report == null)
                return false;

            _context.ProductReports.Remove(report);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
