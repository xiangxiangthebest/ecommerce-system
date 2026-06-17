using EcommerceSystem.Data;
using EcommerceSystem.Models;
using Microsoft.EntityFrameworkCore;
using EcommerceSystem.Interfaces;

namespace EcommerceSystem.Services;

/// <summary>
/// Runs in the background every hour.
/// Any order that has been DELIVERED for more than 3 days and has not yet
/// been manually confirmed by the customer is automatically moved to RECEIVED.
///
/// Also handles orders that went through after-sales (return/refund) flow:
///   RETURN_REFUND_REJECTED → auto-RECEIVED after grace period (CS rejected, order stays with customer)
///   RETURN_REFUND          → auto-RECEIVED after grace period (Return & Refund approved by CS)
///   REFUND                 → auto-RECEIVED after grace period (Refund-Only approved by CS)
/// </summary>
public class AutoReceiveOrdersJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutoReceiveOrdersJob> _logger;

    // How often to check (every 1 hour is fine; reduce to minutes for testing)
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    // How many days after DELIVERED before auto-confirming
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
            // This catches the TaskCanceledException gracefully when the app stops.
            // It prevents the debugger from breaking and throwing a crash screen.
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

        // Eligible statuses for auto-receive:
        //   DELIVERED            → normal delivery, customer never manually confirmed
        //   RETURN_REFUND_REJECTED → CS rejected the request; order stays with customer → auto-close
        //   RETURN_REFUND        → CS approved a Return & Refund request → auto-close
        //   REFUND               → CS approved a Refund-Only request     → auto-close
        var overdueOrders = await db.Order
            .Where(o =>
                (o.CurrentStatus == OrderStatus.DELIVERED         ||
                 o.CurrentStatus == OrderStatus.RETURN_REFUND_REJECTED ||
                 o.CurrentStatus == OrderStatus.RETURN_REFUND     ||  // Return & Refund approved by CS
                 o.CurrentStatus == OrderStatus.REFUND)               // Refund-Only approved by CS
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