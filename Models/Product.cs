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

        // Primary image path (first image, kept for backward compatibility)
        public string ImagePath { get; set; } = string.Empty;

        // JSON array of all image paths in display order
        // e.g. ["/images/a.jpg", "/images/b.jpg", "/images/c.jpg"]
        public string ImagePathsJson { get; set; } = "[]";

        public int SellerId { get; set; }
        public int CategoryId { get; set; }

        // Track if product is a draft or published
        public bool IsDraft { get; set; } = false;

        // Stores variation groups as JSON string
        // e.g. [{name:"Flavour", values:[{label:"Original", stock:10, imagePath:"/images/x.jpg"}]}]
        public string VariationsJson { get; set; } = "[]";

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
    }
}
