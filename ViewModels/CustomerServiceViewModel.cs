using EcommerceSystem.DTOs;

namespace EcommerceSystem.ViewModels;

public class CustomerServiceViewModel
{
    public List<ReturnOrderDto> Orders { get; set; } = new();
}