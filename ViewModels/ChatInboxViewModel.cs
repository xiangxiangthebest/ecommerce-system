namespace EcommerceSystem.ViewModels
{
    public class ChatInboxViewModel
    {
        public int ConversationId { get; set; }

        public int ProductId { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public string? ProductImage { get; set; }

        public string BuyerName { get; set; } = string.Empty;

        public string? VariationText { get; set; }

        public string LastMessage { get; set; } = string.Empty;

        public DateTime LastMessageTime { get; set; }

        public int UnreadCount { get; set; }
    }
}