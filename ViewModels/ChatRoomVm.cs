using EcommerceSystem.Models;
namespace EcommerceSystem.ViewModels
{
    public class ChatRoomVm
    {
        public ChatRoom ChatRoom { get; set; } = new();
        public Customer Customer { get; set; }
        public Seller Seller { get; set; }
        public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }
}