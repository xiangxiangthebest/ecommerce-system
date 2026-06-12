namespace EcommerceSystem.DTOs;

public class ReturnItemDto
{
    public int OrderItemId { get; set; }

    public string Name { get; set; } = "";

    public int OrderedQty { get; set; }

    public int RequestedQty { get; set; }

    public string UnitPrice { get; set; } = "";

    public string SelectedVariation { get; set; } = "";
}