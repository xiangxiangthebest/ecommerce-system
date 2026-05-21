using Microsoft.AspNetCore.Http;

namespace EcommerceSystem.Interfaces
{
    public interface IReviewImageStorage
    {
        Task<List<string>> SaveReviewImagesAsync(List<IFormFile> files);
    }
}