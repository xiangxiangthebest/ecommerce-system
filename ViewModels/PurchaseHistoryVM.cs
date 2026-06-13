namespace EcommerceSystem.Models.ViewModels
{
    public class PurchaseHistoryVM
    {
        public List<Order> Orders { get; set; } = new();
        public List<Request> Requests { get; set; } = new();
        public Dictionary<int, Request> RequestMap { get; set; } = new();
    }
}