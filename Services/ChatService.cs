using EcommerceSystem.Data;
using EcommerceSystem.Interfaces;
using EcommerceSystem.Models;
using EcommerceSystem.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EcommerceSystem.Services
{
    public class ChatService : IChatService
    {
        private readonly AppDbContext _context;

        public ChatService(AppDbContext context)
        {
            _context = context;
        }

    public async Task<ChatRoom> GetChatRoomAsync(int sellerId, int customerId, int? productId)
    {
        var chatRoom = await _context.ChatRoom
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.SellerId == sellerId);

            if (chatRoom != null)
                return chatRoom;
            
        chatRoom = new ChatRoom
        {
            ChatRoomId = 0,
            CustomerId = customerId,
            SellerId = sellerId,
            Messages = new List<ChatMessage>()
        };

        return chatRoom;
    }
            
        public async Task SendMessageAsync(int chatRoomId, int senderId, string message)
        {
            var newMessage = new ChatMessage
            {
                ChatRoomId = chatRoomId,
                SenderId = senderId,
                MessageText = message,
                SentAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.ChatMessages.Add(newMessage);

            await _context.SaveChangesAsync();
        }

        public async Task<List<ChatBoxListMV>> GetSellerInboxListAsync(int sellerId)
        {
            var inboxList = await _context.ChatRoom
                .Where(c => c.SellerId == sellerId)
                .Select(c => new ChatBoxListMV
                {
                    ChatRoomId = c.ChatRoomId,
                    CustomerId = c.CustomerId,
                    CustomerName = c.Customer != null ? c.Customer.FullName : null,

                    LastMessage = c.Messages
                        .OrderByDescending(m => m.SentAt)
                        .Select(m => m.MessageText)
                        .FirstOrDefault(),

                    LastMessageTime = c.Messages
                        .OrderByDescending(m => m.SentAt)
                        .Select(m => m.SentAt)
                        .FirstOrDefault(),

                    UnreadCount = c.Messages
                        .Count(m => !m.IsRead && m.SenderId != sellerId)
                })
                .OrderByDescending(x => x.LastMessageTime)
                .ToListAsync();

            return inboxList;
        }

        public async Task<List<ChatBoxListMV>> GetCustomerInboxListAsync(int customerId)
        {
            var inboxList = await _context.ChatRoom
                .Where(c => c.CustomerId == customerId)
                .Select(c => new ChatBoxListMV
                {
                    ChatRoomId = c.ChatRoomId,
                    CustomerId = c.CustomerId,
                    CustomerName = c.Customer != null ? c.Customer.FullName : null,

                    SellerId = c.SellerId,
                    SellerName = c.Seller != null ? c.Seller.FullName : null,

                    LastMessage = c.Messages
                        .OrderByDescending(m => m.SentAt)
                        .Select(m => m.MessageText)
                        .FirstOrDefault(),

                    LastMessageTime = c.Messages
                        .OrderByDescending(m => m.SentAt)
                        .Select(m => m.SentAt)
                        .FirstOrDefault(),

                    UnreadCount = c.Messages
                        .Count(m => !m.IsRead && m.SenderId != customerId)
                })
                .OrderByDescending(x => x.LastMessageTime)
                .ToListAsync();

            return inboxList;
        }
        
        public async Task<ChatRoom?> GetChatRoomByIdAsync(int chatRoomId)
        {
            return await _context.ChatRoom
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.ChatRoomId == chatRoomId);
        }
    }
}