namespace EcommerceSystem.Enums;  // ← change from EcommerceSystem.Models

/// <summary>
/// The type of return/refund the customer is requesting.
/// </summary>
public enum ReturnType
{
    RefundOnly   = 0,
    ReturnRefund = 1,
}