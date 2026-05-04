using EcommerceSystem.DTOs;
using EcommerceSystem.Interfaces;
using EcommerceSystem.Models;

namespace EcommerceSystem.Factories
{
    public class CustomerFactory : IUserFactory
    {
        private readonly RegisterCustomerDto _dto;

        public CustomerFactory(RegisterCustomerDto dto)
        {
            _dto = dto;
        }

        public User CreateUser()
        {
            return new Customer
            {
                FullName = _dto.FullName,
                Email = _dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(_dto.Password),
                Role = "Customer"
            };
            
        }
    }
}