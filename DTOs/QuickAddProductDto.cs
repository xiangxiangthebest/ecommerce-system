using EcommerceSystem.DTOs;

namespace EcommerceSystem.DTOs
{
    public class QuickAddProductDto
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = "";
        public double Price { get; set; }
        public double OriginalPrice { get; set; }
        public string SKU { get; set; } = "";
        public string Description { get; set; } = "";
        public int StockQuantity { get; set; }
        public List<string> Images { get; set; } = new();
        public List<VariationGroupDto> Variations { get; set; } = new();
        public string VariationCombosJson { get; set; } = "[]";
        public string ShopName { get; set; } = "Unknown Shop";
    }
}