using EcommerceSystem.Models;
using EcommerceSystem.Models.ViewModels;
using Microsoft.AspNetCore.Http;

namespace EcommerceSystem.Interfaces
{
    public interface IProfileService
    {
        Task<Customer?> GetProfileAsync(int customerId);
        Task<OperationResult<User>> UpdateProfileAsync(int customerId, Customer updated, IFormFile? profileImage);
        Task<OperationResult> AddAddressAsync(int customerId, DeliveryField address);
        Task<OperationResult> EditAddressAsync(int customerId, DeliveryField address);
        Task<OperationResult> RemoveAddressAsync(int customerId, int addressId);
        Task<OperationResult> ChangePasswordAsync(int customerId, string oldPassword, string newPassword);
    }
}