using EcommerceSystem.Interfaces;

namespace EcommerceSystem.Services;

public class AutoReceiveOrdersJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutoReceiveOrdersJob> _logger;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    // auto receive triggered after 3 days (72hours, that checking every hour) once the status changes/updates to DELIVERED, RETURN_REFUND, or REFUND
     private const int AutoReceiveDays = 3;

    public AutoReceiveOrdersJob(
        IServiceScopeFactory scopeFactory,
        ILogger<AutoReceiveOrdersJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AutoReceiveOrdersJob started.");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "AutoReceiveOrdersJob encountered an error during execution.");
                }

                // This delay is now safely guarded against application shutdown exceptions
                await Task.Delay(CheckInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the application is shutting down; no action needed
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AutoReceiveOrdersJob encountered an unexpected error.");
        }
        finally
        {
            _logger.LogInformation("AutoReceiveOrdersJob stopped.");
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var cutoff = DateTime.UtcNow.AddDays(-AutoReceiveDays);

        // Status that eligible for the triggering the auto-receive (Delivered, Return & Refund, Refund-Only)
        var overdueOrders = await db.Order
            .Where(o =>
                (o.CurrentStatus == OrderStatus.DELIVERED) 
                && o.DeliveredAt != null
                && o.DeliveredAt <= cutoff)
            .Include(o => o.Customer)
            .Include(o => o.Seller)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .ToListAsync(ct);

        if (overdueOrders.Count == 0) return;

        _logger.LogInformation(
            "AutoReceiveOrdersJob: {Count} order(s) will be auto-confirmed as RECEIVED.",
            overdueOrders.Count);

        var adminIds = await db.Users
            .Where(u => u.Role == "Admin" && u.IsActive)
            .Select(u => u.UserId)
            .ToListAsync(ct);

        foreach (var order in overdueOrders)
        {
            order.CurrentStatus = OrderStatus.RECEIVED;
            order.ReceivedAt    = DateTime.UtcNow;

            _logger.LogInformation(
                "AutoReceiveOrdersJob: Order #{OrderId} auto-moved to RECEIVED " +
                "(was {PreviousStatus}, DeliveredAt {DeliveredAt}).",
                order.OrderId, order.CurrentStatus, order.DeliveredAt);

            // Send RECEIVED notifications to customer, seller, and all admins
            try
            {
                await SendReceivedNotificationsAsync(order, adminIds, notificationService);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "AutoReceiveOrdersJob: Notification failed for Order #{OrderId}.", order.OrderId);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task SendReceivedNotificationsAsync(
        Order order,
        List<int> adminIds,
        INotificationService notificationService)
    {
        var orderId      = order.OrderId;
        var shopName     = order.Seller?.ShopName ?? "the seller";
        var customerId   = order.Customer?.UserId;
        var customerName = order.Customer?.FullName ?? "Customer";
        var sellerId     = order.Seller?.UserId;

        // 1. Customer
        if (customerId.HasValue)
            await notificationService.CreateAsync(
                userId:  customerId.Value,
                title:   "Order Completed",
                message: $"Order #{orderId} from {shopName} has been automatically marked as completed. " +
                         $"Thank you for your purchase! You can now rate and review the product."
            );

        // 2. Seller
        if (sellerId.HasValue)
            await notificationService.CreateAsync(
                userId:  sellerId.Value,
                title:   "Order Received",
                message: $"Order #{orderId} has been automatically confirmed as received by the system. " +
                         $"Transaction complete."
            );

        // 3. Admins
        foreach (var adminId in adminIds)
            await notificationService.CreateAsync(
                userId:  adminId,
                title:   "Order Received by Customer",
                message: $"Customer {customerName} — Order #{orderId} from {shopName} " +
                         $"was auto-confirmed as RECEIVED by the system."
            );
    }
}