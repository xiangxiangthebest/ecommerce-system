using EcommerceSystem.Data;
using EcommerceSystem.Interfaces;
using EcommerceSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace EcommerceSystem.Services
{
    public class ReviewService : IReviewService
    {
        private readonly AppDbContext _context;

        public ReviewService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<OperationResult> SubmitRatingAsync(int customerId, int orderItemId, int rating, string reviewText)
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

            var review = new Review
            {
                OrderItemId = orderItemId,
                ProductId = orderItem.ProductId,
                CustomerId = customerId,
                Rating = rating,
                ReviewText = reviewText?.Trim() ?? "",
                CreatedAt = DateTime.UtcNow
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