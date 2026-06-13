namespace EcommerceSystem.Models;

public enum OrderStatus
{
    PENDING,
    PREPARING,
    SHIPPED,
    DELIVERED,
    RECEIVED,
    CANCELED,
    CANCEL_REQUESTED,
    RETURN_REFUND_REQUESTED,
    RETURN_REFUND,
    RETURN_REFUND_REJECTED,
    REFUND
}