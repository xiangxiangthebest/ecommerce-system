namespace EcommerceSystem.Models;
using System.ComponentModel.DataAnnotations;
public class Customer : User
{
    [Required]
    public string Phone { get; set; } = string.Empty;
    [Required]
    public string Address { get; set; } = string.Empty;
    [Required]
    public string Gender { get; set; } = string.Empty;
    [Required]
    public DateTime? Birthday { get; set; }
    public string ProfilePicture { get; set; }
}