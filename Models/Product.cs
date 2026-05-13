using System.Text.Json;

namespace EcommerceSystem.Models
{
    public class Product
    {
        public int ProductId { get; set; }

        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Price { get; set; }
        public int StockQuantity { get; set; }

        public string SKU { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;
        public int SellerId { get; set; }
        public int CategoryId { get; set; }

        // Track if product is a draft or published
        public bool IsDraft { get; set; } = false;

        public Category? Category { get; set; }
        public Seller? Seller { get; set; }

        public Product() { }

        public Product(int productId, string name, string description, double price, int stockQuantity, Category category, Seller seller)
        {
            ProductId = productId;
            Name = name;
            Description = description;
            Price = price;
            StockQuantity = stockQuantity;
            Category = category;
            Seller = seller;
        }

        public bool IsInStock()
        {
            return StockQuantity > 0;
        }

        public double OriginalPrice { get; set; } = 0;
        public double AverageRating { get; set; } = 0;
        public double ReviewCount { get; set; } = 0;

        public class VariationGroup
        {
            public string Name { get; set; } = "";
            public List<VariationValue> Values { get; set; } = new();
        }

        public class VariationValue
        {
            public string Label { get; set; } = "";
            public int Stock { get; set; }
            public string ImagePath { get; set; } = "";
        }
    }
}