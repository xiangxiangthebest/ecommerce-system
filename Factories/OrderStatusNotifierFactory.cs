using EcommerceSystem.Data;
using EcommerceSystem.Interfaces;
using EcommerceSystem.Observers;

namespace EcommerceSystem.Factories;

/// <summary>
/// Builds a fully-wired OrderStatusNotifier with all three observers attached.
/// Inject this factory wherever you need to trigger order-status notifications
/// (e.g. your OrderService).
/// </summary>
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
        notifier.Attach(new CustomerDashboardObserver(_notificationService));
        notifier.Attach(new SellerDashboardObserver(_notificationService));
        notifier.Attach(new AdminPanelObserver(_notificationService, _context));

        return notifier;
    }
}
