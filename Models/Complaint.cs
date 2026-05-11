using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceSystem.Models
{
    public class Complaint
    {
        [Key]
        public int ComplaintId { get; set; }

        public int OrderId { get; set; }

        public int UserId { get; set; }

        public string Message { get; set; } = string.Empty;

        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("OrderId")]
        public Order? Order { get; set; }
    }
}
