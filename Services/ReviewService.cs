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
                                        && oi.Order != null
                                        && oi.Order.CustomerUserId == customerId
                                        && oi.Order.CurrentStatus == OrderStatus.RECEIVED);

            if (orderItem == null)
                return OperationResult.Fail("Cannot review this item.");

            // One review per Order per Product (not per customer lifetime)
            var existing = await _context.Reviews
                .Include(r => r.OrderItem)
                .FirstOrDefaultAsync(r => r.OrderItem != null
                                    && r.OrderItem.OrderId   == orderItem.OrderId
                                    && r.OrderItem.ProductId == orderItem.ProductId
                                    && r.CustomerId          == customerId);

            if (existing != null)
            {
                // Already reviewed this product for this order (e.g. submitting a rating
                // that covers multiple variations/order items of the same product).
                // Treat as a no-op success so the UI doesn't show "could not be saved",
                // but still let this orderItemId count toward "all items reviewed".
                var allOrderItemsDup = await _context.OrderItems
                    .Where(oi => oi.OrderId == orderItem.OrderId)
                    .Select(oi => oi.OrderItemId)
                    .ToListAsync();

                var reviewedItemIdsDup = await _context.Reviews
                    .Include(r => r.OrderItem)
                    .Where(r => r.CustomerId == customerId && r.OrderItem != null && r.OrderItem.OrderId == orderItem.OrderId)
                    .Select(r => r.OrderItemId)
                    .ToListAsync();

                reviewedItemIdsDup.Add(orderItemId);

                bool allReviewedDup = allOrderItemsDup.All(id => reviewedItemIdsDup.Contains(id));
                if (allReviewedDup)
                {
                    var orderDup = orderItem.Order;
                    if (orderDup != null)
                    {
                        orderDup.ReviewSubmitted = true;
                        _context.Order.Update(orderDup);
                        await _context.SaveChangesAsync();
                    }
                }

                return OperationResult.Ok();
            }

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
                OrderItemId  = orderItemId,
                ProductId    = orderItem.ProductId,
                CustomerId   = customerId,
                Rating       = rating,
                ReviewText   = reviewText?.Trim() ?? "",
                CreatedAt    = DateTime.UtcNow,
                ReviewImagePathsJson = System.Text.Json.JsonSerializer.Serialize(imagePaths)
            };

            _context.Reviews.Add(review);

            var product = await _context.Products.FindAsync(orderItem.ProductId);
            if (product != null)
            {
                var allRatings = await _context.Reviews
                    .Include(r => r.OrderItem)
                    .Where(r => r.ProductId == orderItem.ProductId && r.OrderItem != null)
                    .Select(r => new { r.OrderItem!.OrderId, r.Rating })
                    .ToListAsync();

                allRatings.Add(new { OrderId = orderItem.OrderId, Rating = rating });

                var deduplicatedRatings = allRatings
                    .GroupBy(r => r.OrderId)
                    .Select(g => g.First().Rating)
                    .ToList();

                product.AverageRating = deduplicatedRatings.Average();
                product.ReviewCount   = deduplicatedRatings.Count;
            }

            // Check if all items in this order now have a review — if so, mark order as ReviewSubmitted
            var allOrderItems = await _context.OrderItems
                .Where(oi => oi.OrderId == orderItem.OrderId)
                .Select(oi => oi.OrderItemId)
                .ToListAsync();

            var reviewedItemIds = await _context.Reviews
                .Where(r => r.CustomerId == customerId && allOrderItems.Contains(r.OrderItemId))
                .Select(r => r.OrderItemId)
                .ToListAsync();

            // Include the current review being submitted
            reviewedItemIds.Add(orderItemId);

            bool allReviewed = allOrderItems.All(id => reviewedItemIds.Contains(id));

            if (allReviewed)
            {
                var order = orderItem.Order;
                if (order != null)
                {
                    order.ReviewSubmitted = true;
                    _context.Order.Update(order);
                }
            }

            await _context.SaveChangesAsync();
            return OperationResult.Ok();
        }
    }
}