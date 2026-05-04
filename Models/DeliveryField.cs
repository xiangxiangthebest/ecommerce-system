namespace EcommerceSystem.Models;

public class DeliveryField
{
    public int DeliveryFieldId { get; set; }
    public string Address { get; set; }
    public string PhoneNumber { get; set; }
    public string Name { get; set; }
    public bool IsDefault { get; set; }

}

// Customer c = new Customer();

// c.DeliveryFields.Add(new DeliveryField
// {
//     Address = "KL, Malaysia",
//     PhoneNumber = "0123456789",
//     ReceiverName = "Tam Xin Yi"
// });