namespace EcommerceSystem.Models
{
    public class ChatRoom
    {
        public int ChatRoomId { get; set; }
        public int CustomerId { get; set; }
        public Customer Customer { get; set; }
        public int SellerId { get; set; }
        public Seller Seller { get; set; }
        // public int? ProductId { get; set; }
        // public Product? Product { get; set; }
        public DateTime CreatedAt { get; set; }
        public ICollection<ChatMessage> Messages { get; set; }
            = new List<ChatMessage>();
    }
}