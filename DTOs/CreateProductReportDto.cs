namespace EcommerceSystem.DTOs;

public class CreateProductReportDto
{
    public int ProductId { get; set; }
    public string ReportReason { get; set; } = string.Empty;
    public string ReportDescription { get; set; } = string.Empty;
}
