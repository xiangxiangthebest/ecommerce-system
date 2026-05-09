namespace EcommerceSystem.Models
{
    public class Seller : User
    {
        // REMOVED: UserId (It is already inherited from the User class)

        public string NRICNumber { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string DetailAddress { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string TIN { get; set; } = string.Empty;
        public string ShopName { get; set; } = string.Empty;
        public string PickupAddress { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public int SoldItemCount { get; set; }

        public bool IsApproved { get; set; } = false;

        public List<Product> Products { get; set; } = new();
        public List<Order> Orders { get; set; } = new();
    }
}