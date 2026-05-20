namespace EcommerceSystem.Models
{
    public class ChatConversation
    {
        public int ChatConversationId { get; set; }

        public int CustomerId { get; set; }
        public User Customer { get; set; }

        public int SellerId { get; set; }
        public Seller Seller { get; set; }

        // Product context
        public int ProductId { get; set; }
        public Product Product { get; set; }

        // Variation info
        public string? VariationJson { get; set; }

        public DateTime CreatedAt { get; set; }

        public ICollection<ChatMessage> Messages { get; set; }
            = new List<ChatMessage>();
    }
}