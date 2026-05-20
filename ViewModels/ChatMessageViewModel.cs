namespace EcommerceSystem.ViewModels
{
    public class ChatMessageViewModel
    {
        public int SenderId { get; set; }

        public string SenderName { get; set; } = string.Empty;

        public string MessageText { get; set; } = string.Empty;

        public DateTime SentAt { get; set; }

        public bool IsMine { get; set; }
    }
}