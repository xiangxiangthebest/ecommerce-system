using System.ComponentModel.DataAnnotations;

namespace EcommerceSystem.DTOs
{
    public class RegisterCustomerServiceDto
    {
        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [RegularExpression(@"^[a-zA-Z0-9._%+\-]+@gmail\.com$", ErrorMessage = "Email must be a valid @gmail.com address.")]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^\d+$", ErrorMessage = "Phone number must contain digits only.")]
        public string PhoneNumber { get; set; } = string.Empty;

    }
}