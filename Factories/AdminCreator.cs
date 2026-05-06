using EcommerceSystem.Interfaces;
using EcommerceSystem.Models;

namespace EcommerceSystem.Factories
{
    public class AdminCreator : UserCreator
    {
        public override User CreateUser()
        {
            return new Admin
            {
                FullName = "Administrator",
                Email = "admin@gmail.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Role = "Admin"
            };
        }
    }
}