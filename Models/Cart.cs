namespace EcommerceSystem.Models;

public class Cart
{
    public int CartId { get; set; }
    public int UserId { get; set; }
    // Cart contains products with extra attributes
    public List<CartProduct> products{ get; set; }
    public List<CartItem> CartItems { get; set; } = new();

        // public Cart()
        // {
        //     products = new List<CartProduct>();
        // }

        // public Cart(List<CartProduct> products)
        // {
        //     this.products = products;
        // }

        // public List<CartProduct> GetProducts()
        // {
        //     return products;
        // }

        // public void SetProducts(List<CartProduct> products)
        // {
        //     this.products = products;
        // }

        // public decimal CalculateTotal()
        // {
        //     return products.Sum(cp => cp.price * cp.quantity);
        // }
}