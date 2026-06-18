namespace EcommerceSystem.Models;

public class RequestImage
{
    public int Id { get; set; }
    public int RequestId { get; set; }
    public Request? Request { get; set; }
    public string? ImagePath { get; set; }
}