using EcommerceSystem.Interfaces;
using Microsoft.AspNetCore.Http;

namespace EcommerceSystem.Services
{
    public class LocalReportImageStorage : IReportImageStorage
    {
        public async Task<List<string>> SaveReportEvidenceImagesAsync(List<IFormFile> files)
        {
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/reports");

            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var paths = new List<string>();

            foreach (var file in files)
            {
                if (file == null || file.Length == 0) continue;

                var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
                var filePath = Path.Combine(uploadsFolder, fileName);

                using var stream = new FileStream(filePath, FileMode.Create);
                await file.CopyToAsync(stream);

                paths.Add("/images/reports/" + fileName);
            }

            return paths;
        }
    }
}
