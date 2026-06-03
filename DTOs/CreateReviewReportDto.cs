namespace EcommerceSystem.DTOs;

public class CreateReviewReportDto
{
    public int ReviewId { get; set; }
    public string ReportReason { get; set; } = string.Empty;
    public string ReportDescription { get; set; } = string.Empty;
}
