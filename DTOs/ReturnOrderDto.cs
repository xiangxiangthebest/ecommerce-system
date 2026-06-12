namespace EcommerceSystem.DTOs;

public class ReturnOrderDto
{
    public int OrderId { get; set; }

    public string ReturnToken { get; set; } = "";

    public string ServiceLabel { get; set; } = "";

    public string IssueLabel { get; set; } = "";

    public List<string> ReturnImages { get; set; } = new();

    public string CustomerEmail { get; set; } = "";

    public string CustomerName { get; set; } = "";

    public List<ReturnItemDto> Items { get; set; } = new();
}