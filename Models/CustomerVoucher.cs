using EcommerceSystem.Models;

public class CustomerVoucher
{
    public int CustomerVoucherId { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; }
    public int VoucherId { get; set; }
    public Voucher Voucher { get; set; }
    public bool IsUsed { get; set; } = false;
    public DateTime AssignedAt { get; set; } = DateTime.Now;
    public DateTime? UsedAt { get; set; }
}