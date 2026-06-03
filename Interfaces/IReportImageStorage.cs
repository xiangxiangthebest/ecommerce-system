using Microsoft.AspNetCore.Http;

namespace EcommerceSystem.Interfaces
{
    public interface IReportImageStorage
    {
        Task<List<string>> SaveReportEvidenceImagesAsync(List<IFormFile> files);
    }
}
