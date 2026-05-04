namespace EcommerceSystem.Models
{
    public class Product
    {
        public int ProductId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public double Price { get; set; }
        public int StockQuantity { get; set; }
        public Category Category { get; set; }
        public Seller Seller { get; set; }

        // 默认构造函数
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