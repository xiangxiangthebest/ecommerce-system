namespace EcommerceSystem.Models
{
    public class ChatMessage
{
    public int ChatMessageId { get; set; }

    public int ChatConversationId { get; set; }
    public ChatConversation Conversation { get; set; }

    public int SenderId { get; set; }

    public string MessageText { get; set; }

    public DateTime SentAt { get; set; } = DateTime.Now;

    public bool IsRead { get; set; } = false;
}
}