namespace EcommerceSystem.Interfaces
{
    public interface IReviewService
    {
        Task<OperationResult> SubmitRatingAsync(int customerId, int orderItemId, int rating, string reviewText, List<IFormFile> images);
    }
}