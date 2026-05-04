namespace EcommerceSystem.Models;

public class User
{
    public int UserId { get; set; }
    public string Role { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public string PasswordHash { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime DateJoin { get; set; }
}