using EcommerceSystem.Models;

namespace EcommerceSystem.Interfaces
{
    public interface IOrderService
    {
        Task<OperationResult> PlaceOrderAsync(PlaceOrderRequest request);
        Task<List<Order>> GetPurchaseHistoryAsync(int customerId);
        Task<OperationResult> CancelOrderAsync(int customerId, int orderId, string reason);
        Task<OperationResult> ConfirmReceivedAsync(int customerId, int orderId);
        Task<OperationResult> SubmitComplaintAsync(int customerId, int orderId, string complaintText);
        Task<OperationResult> UpdateOrderStatusAsync(int orderId, OrderStatus status);
    }
}