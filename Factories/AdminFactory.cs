using EcommerceSystem.Interfaces;
using EcommerceSystem.Models;

namespace EcommerceSystem.Factories
{
    public class AdminFactory : IUserFactory
    {
        public User CreateUser()
        {
            return new User
            {
                FullName = "Administrator",
                Email = "admin@gmail.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Role = "Admin"
            };
        }
    }
}