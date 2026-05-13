using Microsoft.EntityFrameworkCore;
using PropertyLeasing.API.Data;

namespace PropertyLeasing.MVC.Services;

/// <summary>
/// Background service that runs once at startup and then daily at midnight.
/// It marks units as "UnderMaintenance" when their scheduled pre-tenancy
/// maintenance date has arrived and the request is still open.
/// </summary>
public class MaintenanceDailyService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MaintenanceDailyService> _logger;

    public MaintenanceDailyService(
        IServiceScopeFactory scopeFactory,
        ILogger<MaintenanceDailyService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run immediately on startup to catch any missed days
        await ApplyScheduledMaintenanceAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            // Sleep until next midnight
            var now       = DateTime.Now;
            var nextRun   = now.Date.AddDays(1); // 00:00:00 tomorrow
            var delay     = nextRun - now;
            _logger.LogInformation("MaintenanceDailyService: next run at {NextRun}", nextRun);

            await Task.Delay(delay, stoppingToken);

            if (!stoppingToken.IsCancellationRequested)
                await ApplyScheduledMaintenanceAsync(stoppingToken);
        }
    }

    private async Task ApplyScheduledMaintenanceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PropertyLeasingDbContext>();

            var today = DateTime.Today;

            // Find all pre-tenancy requests whose work day has arrived and are still open
            var dueRequests = await db.MaintenanceRequests
                .Include(r => r.Unit)
                .Where(r => r.ScheduledDate.HasValue
                         && r.ScheduledDate.Value.Date <= today
                         && r.Status != "Cancelled"
                         && r.Status != "Resolved"
                         && r.Status != "Closed"
                         && r.Unit != null
                         && r.Unit.AvailabilityStatus != "UnderMaintenance")
                .ToListAsync(ct);

            if (dueRequests.Count == 0)
            {
                _logger.LogInformation("MaintenanceDailyService: no units to update today ({Today})", today);
                return;
            }

            foreach (var req in dueRequests)
            {
                req.Unit!.AvailabilityStatus = "UnderMaintenance";
                _logger.LogInformation(
                    "MaintenanceDailyService: Unit {UnitId} → UnderMaintenance (ticket {Ticket}, scheduled {Date})",
                    req.UnitId, req.TicketNumber, req.ScheduledDate!.Value.ToString("dd MMM yyyy"));
            }

            await db.SaveChangesAsync(ct);
            _logger.LogInformation("MaintenanceDailyService: updated {Count} unit(s) to UnderMaintenance", dueRequests.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "MaintenanceDailyService: error applying scheduled maintenance");
        }
    }
}
