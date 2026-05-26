using Microsoft.AspNetCore.Http;

namespace EcommerceSystem.Interfaces
{
    public interface IProfileImageStorage
    {
        Task<string> SaveProfileImageAsync(IFormFile file);
    }
}