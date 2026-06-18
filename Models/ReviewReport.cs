namespace EcommerceSystem.Models;

public class ReviewReport
{
    public int ReviewReportId { get; set; }
    public int? ReviewId { get; set; }
    public Review? Review { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string ReportReason { get; set; } = string.Empty;
    public string ReportDescription { get; set; } = string.Empty;
    public string EvidenceImagePathsJson { get; set; } = "[]";
    public string Status { get; set; } = "Pending";
    public DateTime ReportedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string AdminNotes { get; set; } = string.Empty;
    public string? SavedProductName { get; set; }
    public int SavedRating { get; set; }
    public string? SavedReviewText { get; set; }
    public string? SavedReviewerName { get; set; }
}
