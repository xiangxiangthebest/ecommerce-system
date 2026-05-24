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
    // 1. 先查数据库有没有已经存在的房间
    var chatRoom = await _context.ChatRoom
        .Include(c => c.Messages)
        .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.SellerId == sellerId);

    // 2. 如果有，直接返回
    if (chatRoom != null)
        return chatRoom;

    // 3. ❌【核心改动】如果没有，只在内存中 new 出来，绝对不 SaveChanges 存盘！
    chatRoom = new ChatRoom
    {
        ChatRoomId = 0, // 👈 标记为 0，代表这是一个还未落库的虚拟房间
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
                .Where(c => c.CustomerId == customerId) // 过滤当前顾客的聊天室
                .Select(c => new ChatBoxListMV
                {
                    ChatRoomId = c.ChatRoomId,
                    CustomerId = c.CustomerId,
                    CustomerName = c.Customer != null ? c.Customer.FullName : null,

                    SellerId = c.SellerId,
                    SellerName = c.Seller != null ? c.Seller.FullName : null, // 拿到商家的名字显示在列表上

                    // 提取最后一条消息内容
                    LastMessage = c.Messages
                        .OrderByDescending(m => m.SentAt)
                        .Select(m => m.MessageText)
                        .FirstOrDefault(),

                    // 提取最后一条消息的时间用于后续排序
                    LastMessageTime = c.Messages
                        .OrderByDescending(m => m.SentAt)
                        .Select(m => m.SentAt)
                        .FirstOrDefault(),

                    // 统计未读数：必须是未读状态，且发送者【不是】当前顾客自己
                    UnreadCount = c.Messages
                        .Count(m => !m.IsRead && m.SenderId != customerId)
                })
                .OrderByDescending(x => x.LastMessageTime) // 按最后聊天时间倒序排列
                .ToListAsync();

            return inboxList;
        }
        
        public async Task<ChatRoom?> GetChatRoomByIdAsync(int chatRoomId)
        {
            return await _context.ChatRoom
                .Include(c => c.Messages) // 顺带加载出历史消息
                .FirstOrDefaultAsync(c => c.ChatRoomId == chatRoomId);
        }
    }
}