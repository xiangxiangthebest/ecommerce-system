namespace EcommerceSystem.Models;
using System.ComponentModel.DataAnnotations;
public class Customer : User
{
    [Required]
    public string Gender { get; set; } = string.Empty;
    public bool GenderLocked { get; set; }
    public DateTime? Birthday { get; set; }
    public bool BirthdayLocked { get; set; }
    public string? ProfilePicture { get; set; }
    public ICollection<DeliveryField> Addresses { get; set; } = new List<DeliveryField>();
    public ICollection<CustomerVoucher> CustomerVouchers { get; set; } = new List<CustomerVoucher>();
}