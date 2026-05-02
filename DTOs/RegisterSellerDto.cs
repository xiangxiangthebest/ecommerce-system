using System.ComponentModel.DataAnnotations;

namespace EcommerceSystem.DTOs
{
    public class RegisterSellerDto
    {
        // =========================
        // Seller Information
        // =========================

        [Required]
        [StringLength(50)]
        public string FullName { get; set; }

        [Required]
        [RegularExpression(@"^[0-9]{1,12}$")]
        public string NRICNumber { get; set; }

        [Required]
        public string State { get; set; }

        [Required]
        public string PostalCode { get; set; }

        [Required]
        public string DetailAddress { get; set; }

        [Required]
        [StringLength(14)]
        public string TIN { get; set; }

        // =========================
        // Shop Information
        // =========================

        [Required]
        public string ShopName { get; set; }

        [Required]
        public string PickupAddress { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string PhoneNumber { get; set; }

        [Required]
        public string Password { get; set; }

        [Required]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; }
    }
}