using System.ComponentModel.DataAnnotations;

namespace EcommerceSystem.DTOs
{
    public class RegisterSellerDto
    {
        // seller information 
        [Required]
        [StringLength(50)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^[0-9]{1,12}$")]
        public string NRICNumber { get; set; } = string.Empty;

        [Required]
        public string State { get; set; } = string.Empty;

        [Required]
        public string PostalCode { get; set; } = string.Empty;

        [Required]
        public string DetailAddress { get; set; } = string.Empty;

        [Required]
        [StringLength(14)]
        public string TIN { get; set; } = string.Empty;

        // shop information
        [Required]
        public string ShopName { get; set; } = string.Empty;

        [Required]
        public string PickupAddress { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}