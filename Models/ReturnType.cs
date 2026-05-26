namespace EcommerceSystem.Models;

/// <summary>
/// The type of return/refund the customer is requesting.
/// </summary>
public enum ReturnType
{
    /// <summary>
    /// Customer sends item back and gets a full refund (product price + delivery fee returned to customer wallet).
    /// Seller pays: customer return delivery fee + product amount.
    /// </summary>
    ReturnRefund,

    /// <summary>
    /// Customer did not receive the item or items are missing — no physical return needed.
    /// Seller pays: refund for the affected product(s) only.
    /// </summary>
    RefundOnly,
}