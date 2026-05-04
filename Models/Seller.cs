namespace EcommerceSystem.Models
{
    public class Seller : User
    {
        public int UserId { get; set; }
        public string NRICNumber { get; set; }
        public string State { get; set; }
        public string DetailAddress { get; set; }
        public string PostalCode { get; set; }
        public string TIN { get; set; }
        public string ShopName { get; set; }
        public string PickupAddress { get; set; }
        public string PhoneNumber { get; set; }
        public int SoldItemCount { get; set; }
        public List<Product> Products { get; set; }
        public List<Order> Orders { get; set; }

    }
}