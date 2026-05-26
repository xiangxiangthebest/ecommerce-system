using EcommerceSystem.Models;
using EcommerceSystem.ViewModels;

namespace EcommerceSystem.Interfaces
{
    public interface ICartService
    {
        Task<Cart> GetCartAsync(int customerId);
        Task<OperationResult> AddToCartAsync(int customerId, int productId, int quantity, string selectedVariations);
        Task<OperationResult> UpdateQuantityAsync(int customerId, int cartItemId, int quantity);
        Task<OperationResult> RemoveItemAsync(int customerId, int cartItemId);
        Task<OperationResult<Checkout>> BuildCartCheckoutAsync(int customerId, List<int> cartItemIds);
        Task<OperationResult<Checkout>> BuildBuyNowCheckoutAsync(int customerId, int productId, int quantity, string selectedVariations);
        Task<int> GetCartItemCountAsync(int customerId);
    }
}