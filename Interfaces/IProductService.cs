using EcommerceSystem.DTOs;
using EcommerceSystem.Models;
using EcommerceSystem.ViewModels;

namespace EcommerceSystem.Interfaces
{
    public interface IProductService
    {
        Task<List<Category>> GetCategoriesAsync();
        Task<List<Product>> GetBrowseProductsAsync(string? search, int? categoryId);
        Task<QuickAddProductDto?> GetQuickAddProductAsync(int productId);
        Task<ProductDetailsVm?> GetProductDetailsAsync(int productId);
    }
}