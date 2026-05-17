namespace EcommerceSystem.Models;
using System.ComponentModel.DataAnnotations;
public class Customer : User
{
    [Required]
    public string Phone { get; set; } = string.Empty;
    [Required]
    public string Gender { get; set; } = string.Empty;
    public DateTime? Birthday { get; set; }
    public string? ProfilePicture { get; set; }
    public ICollection<DeliveryField> Addresses { get; set; } = new List<DeliveryField>();
}