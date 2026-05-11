namespace EcommerceSystem.Models;
public class Customer : User
{
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public DateTime? Birthday { get; set; }
    public string ProfilePicture { get; set; }
}