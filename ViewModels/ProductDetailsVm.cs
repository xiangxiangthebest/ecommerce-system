using EcommerceSystem.DTOs;
using EcommerceSystem.Models;

namespace EcommerceSystem.ViewModels
{
    public class ProductDetailsVm
    {
        public Product Product { get; set; } = new();
        public List<Review> Reviews { get; set; } = new();
        public List<string> Images { get; set; } = new();
        public List<VariationGroupDto> Variations { get; set; } = new();
    }
}