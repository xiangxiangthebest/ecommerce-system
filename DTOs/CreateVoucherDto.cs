using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace EcommerceSystem.DTOs
{
    public class CreateVoucherDto : IValidatableObject
    {
        [Required]
        public string Code { get; set; } = string.Empty;

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Type { get; set; } = string.Empty;

        [Required(ErrorMessage = "Discount value is required.")]
        [Range(0, double.MaxValue, ErrorMessage = "Discount value must be a non-negative number.")]
        public decimal? DiscountValue { get; set; }

        [Required(ErrorMessage = "Minimum spend is required.")]
        [Range(0, double.MaxValue, ErrorMessage = "Minimum spend must be a non-negative number.")]
        public decimal? MinimumSpend { get; set; }

        [Required(ErrorMessage = "Start date is required.")]
        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [Required(ErrorMessage = "End date is required.")]
        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        [Required(ErrorMessage = "Quantity is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int? Quantity { get; set; }

        public bool IsPercentage { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (IsPercentage && DiscountValue.HasValue && DiscountValue.Value > 100m)
            {
                yield return new ValidationResult(
                    "For percentage discounts, the discount value must be 100 or less.",
                    new[] { nameof(DiscountValue) });
            }

            if (StartDate.HasValue && EndDate.HasValue && EndDate.Value < StartDate.Value)
            {
                yield return new ValidationResult(
                    "End date must be the same as or later than the start date.",
                    new[] { nameof(EndDate) });
            }
        }
    }
}
