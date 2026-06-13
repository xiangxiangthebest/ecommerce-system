using System.Text.Json;
using EcommerceSystem.Data;
using EcommerceSystem.DTOs;
using EcommerceSystem.Interfaces;
using EcommerceSystem.Models;
using EcommerceSystem.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EcommerceSystem.Services
{
    public class ProductService : IProductService
    {
        private readonly AppDbContext _context;

        public ProductService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Category>> GetCategoriesAsync()
            => await _context.Category.ToListAsync();

        public async Task<List<Product>> GetBrowseProductsAsync(string? search, int? categoryId)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Seller)
                .Where(p => !p.IsDraft && !p.IsDeleted && p.Seller != null && p.Seller.IsApproved);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var keyword = search.Trim();
                query = query.Where(p =>
                    EF.Functions.Like(p.Name, $"%{keyword}%") ||
                    EF.Functions.Like(p.Seller!.ShopName, $"%{keyword}%"));
            }

            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryId == categoryId.Value);

            var products = await query.ToListAsync();

            var productIds = products.Select(p => p.ProductId).ToList();

            var reviewStats = await _context.Reviews
                .Include(r => r.OrderItem)
                .Where(r => r.OrderItem != null && productIds.Contains(r.ProductId))
                .GroupBy(r => new { r.OrderItem!.OrderId, r.ProductId })
                .Select(g => new { g.Key.ProductId, Rating = g.First().Rating })
                .ToListAsync();

            var statsByProduct = reviewStats
                .GroupBy(r => r.ProductId)
                .ToDictionary(
                    g => g.Key,
                    g => new {
                        Count   = g.Count(),
                        Average = g.Average(r => (double)r.Rating)
                    }
                );

            foreach (var product in products)
            {
                if (statsByProduct.TryGetValue(product.ProductId, out var stats))
                {
                    product.ReviewCount   = stats.Count;
                    product.AverageRating = stats.Average;
                }
                else
                {
                    product.ReviewCount   = 0;
                    product.AverageRating = 0;
                }
            }

            return products;
        }

        public async Task<QuickAddProductDto?> GetQuickAddProductAsync(int productId)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Seller)
                .FirstOrDefaultAsync(p => p.ProductId == productId && !p.IsDraft && !p.IsDeleted);

            if (product == null) return null;

            var images = string.IsNullOrEmpty(product.ImagePathsJson)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(product.ImagePathsJson) ?? new List<string>();

            if (images.Count == 0 && !string.IsNullOrEmpty(product.ImagePath))
                images.Add(product.ImagePath);

            var variations = string.IsNullOrEmpty(product.VariationsJson)
                ? new List<VariationGroupDto>()
                : JsonSerializer.Deserialize<List<VariationGroupDto>>(
                    product.VariationsJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                  ?? new List<VariationGroupDto>();

            return new QuickAddProductDto
            {
                ProductId = product.ProductId,
                Name = product.Name,
                Price = product.Price,
                OriginalPrice = product.OriginalPrice,
                SKU = product.SKU,
                Description = product.Description,
                StockQuantity = product.StockQuantity,
                Images = images,
                Variations = variations,
                VariationCombosJson = product.VariationCombosJson ?? "[]",
                ShopName = product.Seller?.ShopName ?? "Unknown Shop"
            };
        }

        public async Task<ProductDetailsVm?> GetProductDetailsAsync(int productId)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Seller)
                .FirstOrDefaultAsync(p => p.ProductId == productId && !p.IsDraft && !p.IsDeleted);

            if (product == null) return null;

            var reviews = await _context.Reviews
                .Where(r => r.ProductId == productId)
                .Include(r => r.OrderItem)
                    .ThenInclude(oi => oi!.Order)
                        .ThenInclude(o => o!.Customer)
                .Include(r => r.OrderItem)
                    .ThenInclude(oi => oi!.Order)
                        .ThenInclude(o => o!.OrderItems)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var images = string.IsNullOrEmpty(product.ImagePathsJson)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(product.ImagePathsJson) ?? new List<string>();

            var variations = string.IsNullOrEmpty(product.VariationsJson)
                ? new List<VariationGroupDto>()
                : JsonSerializer.Deserialize<List<VariationGroupDto>>(
                    product.VariationsJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                  ?? new List<VariationGroupDto>();

            return new ProductDetailsVm
            {
                Product = product,
                Reviews = reviews,
                Images = images,
                Variations = variations
            };
        }
    }
}