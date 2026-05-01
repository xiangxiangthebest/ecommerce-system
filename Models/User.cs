namespace EcommerceSystem.Models;

public class User
{
    public int UserId { get; set; }

    public string FullName { get; set; }

    public string Email { get; set; }

    public string PasswordHash { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Address { get; set; }

    public int RoleId { get; set; }

    public Role? Role { get; set; }

    public ICollection<Order>? Orders { get; set; }
}