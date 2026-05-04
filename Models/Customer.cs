namespace EcommerceSystem.Models;
public class Customer : User
{
    public List<Order> PreviousOrders { get; set; }
    public Cart? Cart { get; set; }
    public List<DeliveryField> DeliveryFields { get; set; }
    
    public void SetDefaultDeliveryField(int index)
    {
        foreach (var field in DeliveryFields)
        {
            field.IsDefault = false;
        }
        DeliveryFields[index].IsDefault = true;
    }
}