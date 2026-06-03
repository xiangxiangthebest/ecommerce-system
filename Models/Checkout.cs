using EcommerceSystem.Models;

namespace EcommerceSystem.ViewModels;

public class Checkout
{
    public Customer Customer { get; set; } = new();
    public List<CartItem> CartItems { get; set; } = new();
    public int? SelectedAddressId { get; set; }
    public string PaymentMethod { get; set; } = "FPX";
    public string? NoteToSeller { get; set; }
    public List<DeliveryField> Addresses { get; set; } = new();
    public List<CustomerVoucher> AvailableVouchers { get; set; } = new();
}