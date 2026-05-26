using Microsoft.AspNetCore.Http;

namespace EcommerceSystem.Interfaces
{
    public interface IReturnImageStorage
    {
        Task<List<string>> SaveReturnImagesAsync(List<IFormFile> files);
    }
}