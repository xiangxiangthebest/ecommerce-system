namespace EcommerceSystem.Models;

public class User
{
    public int UserId { get; set; }

    public string Role { get; set; }

    public string FullName { get; set; }

    public string Email { get; set; }

    public string PasswordHash { get; set; }

    public string? NRICNumber { get; set; }

    public string? State { get; set; }

    public string? PostalCode { get; set; }

    public string? DetailAddress { get; set; }

    public string? TIN { get; set; }

    public string? ShopName { get; set; }

    public string? PickupAddress { get; set; }

    public string? PhoneNumber { get; set; }
}