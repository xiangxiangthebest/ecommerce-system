namespace EcommerceSystem.Models;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class DeliveryField
{
    [Key]
    public int AddressId { get; set; }
    [Required]
    public int UserId { get; set; }
    [ForeignKey("UserId")]
    public Customer? Customer { get; set; }
    [Required]
    public string RecipientName { get; set; } = string.Empty;
    [Required]
    public string PhoneNumber { get; set; } = string.Empty;
    [Required]
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    [Required]
    public string City { get; set; } = string.Empty;
    [Required]
    public string Postcode { get; set; } = string.Empty;
    [Required]
    public string State { get; set; } = string.Empty;
    public bool IsDefault { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}