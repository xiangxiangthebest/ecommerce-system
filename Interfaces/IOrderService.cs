using EcommerceSystem.Models;
using EcommerceSystem.Enums;

namespace EcommerceSystem.Interfaces
{
    public interface IOrderService
    {
        Task<OperationResult> PlaceOrderAsync(PlaceOrderRequest request);
        Task<List<Order>> GetPurchaseHistoryAsync(int customerId);
        Task<OperationResult> CancelOrderAsync(int customerId, int orderId, string reason);
        Task<OperationResult> RequestCancelOrderAsync(int customerId, int orderId, string reason);
        Task<OperationResult> ConfirmReceivedAsync(int customerId, int orderId);
        Task<OperationResult> RequestReturnRefundAsync(int userId, int orderId, string reason, List<string> imagePaths,ReturnInitiatedBy initiatedBy);
    }
}