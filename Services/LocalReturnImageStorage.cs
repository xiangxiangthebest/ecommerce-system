using EcommerceSystem.Interfaces;
using Microsoft.AspNetCore.Http;

namespace EcommerceSystem.Services
{
    public class LocalReturnImageStorage : IReturnImageStorage
    {
        public async Task<List<string>> SaveReturnImagesAsync(List<IFormFile> files)
        {
            var uploadsFolder = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot/images/returns"
            );

            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var imagePaths = new List<string>();

            foreach (var file in files)
            {
                var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);

                var filePath = Path.Combine(uploadsFolder, fileName);

                using var stream = new FileStream(filePath, FileMode.Create);

                await file.CopyToAsync(stream);

                imagePaths.Add("/images/returns/" + fileName);
            }

            return imagePaths;
        }
    }
}