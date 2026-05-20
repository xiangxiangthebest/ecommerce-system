namespace EcommerceSystem.ViewModels
{
    public class ChatConversationViewModel
    {
        public int ConversationId { get; set; }

            public int BuyerId { get; set; } 
    public int SellerId { get; set; }
        public string BuyerName { get; set; } = string.Empty;

        public string ProductName { get; set; } = string.Empty;

        public string? ProductImage { get; set; }

        public string? VariationText { get; set; }

        public List<ChatMessageViewModel> Messages { get; set; }
            = new();
    }
}