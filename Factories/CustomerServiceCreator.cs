using EcommerceSystem.DTOs;
using EcommerceSystem.Interfaces;
using EcommerceSystem.Models;

namespace EcommerceSystem.Factories
{
    public class CustomerServiceCreator : UserCreator
    {
        private readonly RegisterCustomerServiceDto _dto;

        public CustomerServiceCreator(RegisterCustomerServiceDto dto)
        {
            _dto = dto;
        }

        public override User CreateUser()
        {
            return new CustomerService
            {
                FullName = _dto.FullName,
                Email = _dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(_dto.Password),
                Role = "CustomerService"
            };
        }
    }
}