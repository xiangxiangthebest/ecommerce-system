namespace EcommerceSystem.Models;

public class SupportRequest
{
    public int SupportRequestId { get; set; }

    public int UserId { get; set; }

    public string RequestType { get; set; }

    public string Message { get; set; }

    public string RequestStatus { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}