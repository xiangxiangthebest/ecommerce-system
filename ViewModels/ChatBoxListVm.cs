using EcommerceSystem.DTOs;
using EcommerceSystem.Models;

namespace EcommerceSystem.ViewModels
{
    public class ChatBoxListMV
    {
        public int ChatRoomId { get; set; }
        public int CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public int SellerId { get; set; }
        public string? SellerName { get; set; }
        public string? LastMessage { get; set; }
        public DateTime? LastMessageTime { get; set; }
        public int UnreadCount { get; set; }
    }
}