using EcommerceSystem.Interfaces;
using Microsoft.AspNetCore.Http;

namespace EcommerceSystem.Services
{
    public class LocalProfileImageStorage : IProfileImageStorage
    {
        public async Task<string> SaveProfileImageAsync(IFormFile file)
        {
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/profile");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(uploadsFolder, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            return "/images/profile/" + fileName;
        }
    }
}