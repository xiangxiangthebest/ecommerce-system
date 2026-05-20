using EcommerceSystem.Data;
using EcommerceSystem.Interfaces;
using EcommerceSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace EcommerceSystem.Services
{
    public class ReviewService : IReviewService
    {
        private readonly AppDbContext _context;
        private readonly IReviewImageStorage _imageStorage;

        public ReviewService(AppDbContext context, IReviewImageStorage imageStorage)
        {
            _context = context;
            _imageStorage = imageStorage;
        }

        public async Task<OperationResult> SubmitRatingAsync(int customerId, int orderItemId, int rating, string reviewText, List<IFormFile> images)
        {
            if (rating < 1 || rating > 5)
                return OperationResult.Fail("Rating must be 1–5.");

            var orderItem = await _context.OrderItems
                .Include(oi => oi.Order)
                .FirstOrDefaultAsync(oi => oi.OrderItemId == orderItemId
                                        && oi.Order.CustomerUserId == customerId
                                        && oi.Order.CurrentStatus == OrderStatus.RECEIVED);

            if (orderItem == null)
                return OperationResult.Fail("Cannot review this item.");

            var existing = await _context.Reviews
                .FirstOrDefaultAsync(r => r.OrderItemId == orderItemId && r.CustomerId == customerId);

            if (existing != null)
                return OperationResult.Fail("You have already reviewed this item.");

            if (images != null && images.Count > 4)
                return OperationResult.Fail("Maximum 4 images allowed.");

            var imagePaths = new List<string>();

            if (images?.Count > 0)
            {
                var limited = images.Take(4).ToList();
                imagePaths = await _imageStorage.SaveReviewImagesAsync(limited);
            }

            var review = new Review
            {
                OrderItemId = orderItemId,
                ProductId = orderItem.ProductId,
                CustomerId = customerId,
                Rating = rating,
                ReviewText = reviewText?.Trim() ?? "",
                CreatedAt = DateTime.UtcNow,
                ReviewImagePathsJson = System.Text.Json.JsonSerializer.Serialize(imagePaths)
            };

            _context.Reviews.Add(review);

            var product = await _context.Products.FindAsync(orderItem.ProductId);
            
            if (product != null)
            {
                var ratings = await _context.Reviews
                    .Where(r => r.ProductId == orderItem.ProductId)
                    .Select(r => r.Rating)
                    .ToListAsync();

                ratings.Add(rating);
                product.AverageRating = ratings.Average();
                product.ReviewCount = ratings.Count;
            }

            await _context.SaveChangesAsync();
            return OperationResult.Ok();
        }
    }
}