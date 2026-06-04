using EcommerceSystem.Models;
using EcommerceSystem.DTOs;

public interface IUserService
{
    Task<User?> LoginAsync(LoginDto dto, string role);
    Task<bool> RegisterCustomerAsync(RegisterCustomerDto dto);
    Task<bool> RegisterSellerAsync(RegisterSellerDto dto);
    Task<bool> RegisterCustomerServiceAsync(RegisterCustomerServiceDto dto);
}