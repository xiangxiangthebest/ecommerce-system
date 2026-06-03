using EcommerceSystem.Data;
using EcommerceSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace EcommerceSystem.Services;

/// <summary>
/// Runs in the background every hour.
/// Any order that has been DELIVERED for more than 3 days and has not yet
/// been manually confirmed by the customer is automatically moved to RECEIVED.
/// </summary>
public class AutoReceiveOrdersJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutoReceiveOrdersJob> _logger;

    // How often to check (every 1 hour is fine; reduce to minutes for testing)
    //private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    // How many days after DELIVERED before auto-confirming
    //private const int AutoReceiveDays = 3;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1); // check every 1 min
    private const int AutoReceiveDays = 0;

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

        //var cutoff = DateTime.UtcNow.AddDays(-AutoReceiveDays);
        var cutoff = DateTime.UtcNow.AddMinutes(-2);

        // Find all orders that are still DELIVERED but were delivered over 3 days ago
        var overdueOrders = await db.Order
            .Where(o => o.CurrentStatus == OrderStatus.DELIVERED
                     && o.DeliveredAt != null
                     && o.DeliveredAt <= cutoff)
            .ToListAsync(ct);

        if (overdueOrders.Count == 0) return;

        _logger.LogInformation(
            "AutoReceiveOrdersJob: {Count} order(s) will be auto-confirmed as RECEIVED.", 
            overdueOrders.Count);

        foreach (var order in overdueOrders)
        {
            // We bypass SetStatus() here intentionally: this is a system-triggered
            // transition, not a user action, so we don't need to run observers
            // (which would Console.WriteLine to nowhere useful in background context).
            // Timestamps are still stamped correctly.
            order.CurrentStatus = OrderStatus.RECEIVED;
            order.ReceivedAt    = DateTime.UtcNow;

            _logger.LogInformation(
                "AutoReceiveOrdersJob: Order #{OrderId} auto-moved to RECEIVED " +
                "(delivered at {DeliveredAt}).",
                order.OrderId, order.DeliveredAt);
        }

        await db.SaveChangesAsync(ct);
    }
}