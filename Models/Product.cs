using System.Text.Json;

namespace EcommerceSystem.Models
{
    public class Product
    {
        public int ProductId { get; set; }

        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Price { get; set; }
        public double OriginalPrice { get; set; } = 0;
        public double DiscountPercentage { get; set; } = 0;
        public int StockQuantity { get; set; }

        public string SKU { get; set; } = string.Empty;

        // Primary image path (first image, kept for backward compatibility)
        public string ImagePath { get; set; } = string.Empty;

        // JSON array of all image paths in display order
        // e.g. ["/images/a.jpg", "/images/b.jpg"]
        public string ImagePathsJson { get; set; } = "[]";

        public int SellerId { get; set; }
        public int CategoryId { get; set; }

        public bool IsDraft { get; set; } = false;

        // ── VARIATION GROUPS ─────────────────────────────────────────────────
        // Stores the group names and their option labels (+ optional image per option).
        // Stock is NOT stored here — it lives in VariationCombosJson below.
        //
        // Schema (1 or 2 groups allowed):
        // [
        //   { "name": "Colour", "values": [{ "label": "Black", "imagePath": "" }, ...] },
        //   { "name": "Size",   "values": [{ "label": "M",     "imagePath": "" }, ...] }
        // ]
        //
        // When there are NO variations this stays "[]" and StockQuantity is used directly.
        public string VariationsJson { get; set; } = "[]";

        // ── VARIATION COMBINATIONS ───────────────────────────────────────────
        // Stores stock per combination of option labels.
        // "keys" order matches the group order in VariationsJson.
        //
        // Single-group example  (only Size):
        // [{ "keys": ["M"], "stock": 50 }, { "keys": ["L"], "stock": 20 }]
        //
        // Two-group example  (Colour × Size):
        // [
        //   { "keys": ["Black", "M"], "stock": 10 },
        //   { "keys": ["Black", "L"], "stock": 5  },
        //   { "keys": ["Yellow","M"], "stock": 8  },
        //   { "keys": ["Yellow","L"], "stock": 0  }
        // ]
        public string VariationCombosJson { get; set; } = "[]";

        public Category? Category { get; set; }
        public Seller? Seller { get; set; }

        public Product() { }

        public Product(int productId, string name, string description, double price,
                    int stockQuantity, Category category, Seller seller)
        {
            ProductId = productId;
            Name = name;
            Description = description;
            Price = price;
            StockQuantity = stockQuantity;
            Category = category;
            Seller = seller;
        }

        public bool IsInStock() => StockQuantity > 0;

        public double AverageRating { get; set; } = 0;
        public double ReviewCount   { get; set; } = 0;

        // Soft delete fields
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        // ── Nested types ─────────────────────────────────────────────────────

        /// <summary>Stock entry for one combination, e.g. { keys:["Black","M"], stock:10 }</summary>
        public class VariationCombo
        {
            public List<string> Keys { get; set; } = new();
            public int Stock { get; set; }
        }
    }
}