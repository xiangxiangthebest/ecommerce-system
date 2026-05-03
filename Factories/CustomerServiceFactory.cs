using EcommerceSystem.DTOs;
using EcommerceSystem.Interfaces;
using EcommerceSystem.Models;

namespace EcommerceSystem.Factories
{
    public class CustomerServiceFactory : IUserFactory
    {
        private readonly RegisterCustomerServiceDto _dto;

        public CustomerServiceFactory(RegisterCustomerServiceDto dto)
        {
            _dto = dto;
        }

        public User CreateUser()
        {
            return new User
            {
                FullName = _dto.FullName,
                Email = _dto.Email,
                PhoneNumber = _dto.PhoneNumber,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(_dto.Password),
                Role = "CustomerService"
            };
        }
    }
}