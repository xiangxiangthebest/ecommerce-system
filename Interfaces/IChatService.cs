using EcommerceSystem.DTOs;
using EcommerceSystem.Models;
using EcommerceSystem.ViewModels;

namespace EcommerceSystem.Interfaces
{
    public interface IChatService
    {
        Task<ChatRoom> GetChatRoomAsync(int sellerId, int customerId, int? productId)
        ;
        Task SendMessageAsync(int chatRoomId, int senderId, string message);
        Task<List<ChatBoxListMV>> GetSellerInboxListAsync(int sellerId);
        Task<List<ChatBoxListMV>> GetCustomerInboxListAsync(int customerId);

        Task<ChatRoom?> GetChatRoomByIdAsync(int chatRoomId);

    }
}