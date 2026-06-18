using EcommerceSystem.Interfaces;
using EcommerceSystem.Observers;

namespace EcommerceSystem.Factories;

public class OrderStatusNotifierFactory
{
    private readonly INotificationService _notificationService;
    private readonly AppDbContext _context;

    public OrderStatusNotifierFactory(
        INotificationService notificationService,
        AppDbContext context)
    {
        _notificationService = notificationService;
        _context = context;
    }

    public OrderStatusNotifier Create()
    {
        var notifier = new OrderStatusNotifier();

        // Attach all three observers
        notifier.Attach(new CustomerNotificationObserver(_notificationService));
        notifier.Attach(new SellerNotificationObserver(_notificationService));
        notifier.Attach(new AdminNotificationObserver(_notificationService, _context));

        return notifier;
    }
}
