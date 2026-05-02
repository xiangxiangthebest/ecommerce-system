using System.ComponentModel.DataAnnotations;

namespace EcommerceSystem.DTOs
{
    public class RegisterCustomerDto
    {
        public string FullName { get; set; }

        public string Email { get; set; }

        public string Password { get; set; }

        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; }
    }
}