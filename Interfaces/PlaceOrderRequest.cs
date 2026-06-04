namespace EcommerceSystem.Interfaces
{
    public class PlaceOrderRequest
    {
        public int CustomerId { get; set; }
        public int SelectedAddressId { get; set; }
        public string PaymentMethod { get; set; } = "FPX";
        public List<int> SelectedItemIds { get; set; } = new();
        public string Source { get; set; } = "cart";
        public int? ProductId { get; set; }
        public int? BuyNowQuantity { get; set; }
        public string BuyNowSelectedVariations { get; set; } = "{}";
        public int? SelectedVoucherId { get; set; }
        public Dictionary<string, string> SellerMessages { get; set; } = new();
    }
}