using EcommerceSystem.DTOs;
using EcommerceSystem.Interfaces;
using EcommerceSystem.Models;

namespace EcommerceSystem.Factories
{
    public class CustomerCreator : UserCreator
    {
        private readonly RegisterCustomerDto _dto;

        public CustomerCreator(RegisterCustomerDto dto)
        {
            _dto = dto;
        }

        public override User CreateUser()
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