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
    AFTER_SALES_REQUESTED,
    RETURN_REFUND,
    REFUND
}
