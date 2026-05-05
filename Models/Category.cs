namespace EcommerceSystem.Models
{
    public class Category
    {
        public int CategoryId { get; set; }        
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;        
        public List<Product> Products { get; set; } = new();
    
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