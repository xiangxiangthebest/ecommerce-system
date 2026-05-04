namespace EcommerceSystem.Models
{
    public class Category
    {
        public int CategoryId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<Product> Products { get; set; }

        // 默认构造函数
        public Category()
        {
            Products = new List<Product>();
        }

        public Category(int categoryId, string name, string description, List<Product> products)
        {
            CategoryId = categoryId;
            Name = name;
            Description = description;
            Products = products;
        }
    }
}