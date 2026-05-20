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

       public async Task<int> StartConversationAsync(int buyerId, int productId, string? variationJson)
{
    var product = await _context.Products
        .Include(p => p.Seller)
        .FirstOrDefaultAsync(p => p.ProductId == productId);

    if (product == null)
        throw new Exception("Product not found");

    var existing = await _context.ChatConversations.FirstOrDefaultAsync(c =>
        c.CustomerId == buyerId &&
        c.ProductId == productId &&
        c.VariationJson == variationJson);

    if (existing != null)
        return existing.ChatConversationId;

    var convo = new ChatConversation
    {
        CustomerId = buyerId,
        SellerId = product.SellerId,
        ProductId = productId,
        VariationJson = variationJson
    };

    _context.ChatConversations.Add(convo);
    await _context.SaveChangesAsync();

    return convo.ChatConversationId;
}

        public async Task SendMessageAsync(int conversationId, int senderId, string message)
        {
            _context.ChatMessages.Add(new ChatMessage
            {
                ChatConversationId = conversationId,
                SenderId = senderId,
                MessageText = message,
                SentAt = DateTime.Now
            });

            await _context.SaveChangesAsync();
        }

        public async Task<ChatConversationViewModel?> GetConversationAsync(int id)
        {
            var convo = await _context.ChatConversations
                .Include(c => c.Product)
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.ChatConversationId == id);

            if (convo == null) return null;

            return new ChatConversationViewModel
            {
                ConversationId = convo.ChatConversationId,
                ProductName = convo.Product.Name,
                ProductImage = convo.Product.ImagePath,
                VariationText = convo.VariationJson,
                Messages = convo.Messages
                    .OrderBy(m => m.SentAt)
                    .Select(m => new ChatMessageViewModel
                    {
                        SenderId = m.SenderId,
                        MessageText = m.MessageText,
                        SentAt = m.SentAt
                    }).ToList()
            };
        }

        public async Task<List<ChatInboxViewModel>> GetSellerInboxAsync(int sellerId)
        {
            return await _context.ChatConversations
                .Where(c => c.SellerId == sellerId)
                .Select(c => new ChatInboxViewModel
                {
                    ConversationId = c.ChatConversationId,
                    ProductName = c.Product.Name,
                    ProductImage = c.Product.ImagePath,
                    VariationText = c.VariationJson,
                    LastMessage = c.Messages
                        .OrderByDescending(m => m.SentAt)
                        .Select(m => m.MessageText)
                        .FirstOrDefault(),
                    LastMessageTime = c.Messages.Max(m => (DateTime?)m.SentAt) ?? DateTime.Now
                })
                .ToListAsync();
        }
    }
}