using EcommerceSystem.Models;
using EcommerceSystem.ViewModels;

namespace EcommerceSystem.Interfaces
{
    //     public interface IChatService
    //     {
    //         Task<int> StartConversationAsync(
    //             int buyerId,
    //             int productId,
    //             string? variationJson);

    //         Task SendMessageAsync(
    //             int conversationId,
    //             int senderId,
    //             string message);

    //         public async Task<List<ChatInboxViewModel>> GetCustomerInboxAsync(int customerId)
    // {
    //     return new List<ChatInboxViewModel>();
    // }

    // public async Task<ChatConversationViewModel?> GetConversationAsync(int conversationId)
    // {
    //     return null;
    // }
    //     }

public interface IChatService
{
    Task<int> StartConversationAsync(int buyerId, int productId, string? variationJson);

    Task SendMessageAsync(int conversationId, int senderId, string message);

    Task<List<ChatInboxViewModel>> GetSellerInboxAsync(int sellerId);

    Task<ChatConversationViewModel?> GetConversationAsync(int conversationId);
}
}