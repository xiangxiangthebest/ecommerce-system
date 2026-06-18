namespace EcommerceSystem.Models;

public class ProductReport
{
    public int ReportId { get; set; }
    public int? ProductId { get; set; }
    public Product? Product { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string ReportReason { get; set; } = string.Empty;
    public string ReportDescription { get; set; } = string.Empty;
    public string EvidenceImagePathsJson { get; set; } = "[]";
    public string Status { get; set; } = "Pending";
    public DateTime ReportedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string AdminNotes { get; set; } = string.Empty;
}
