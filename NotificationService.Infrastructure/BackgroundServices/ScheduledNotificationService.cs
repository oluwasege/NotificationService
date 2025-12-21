using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Interfaces;
using NotificationService.Infrastructure.Data;

namespace NotificationService.Infrastructure.BackgroundServices;

public class ScheduledNotificationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly INotificationQueue _notificationQueue;
    private readonly ILogger<ScheduledNotificationService> _logger;

    public ScheduledNotificationService(
        IServiceProvider serviceProvider,
        INotificationQueue notificationQueue,
        ILogger<ScheduledNotificationService> logger)
    {
        _serviceProvider = serviceProvider;
        _notificationQueue = notificationQueue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scheduled Notification Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessScheduledNotificationsAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing scheduled notifications");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        _logger.LogInformation("Scheduled Notification Service stopped");
    }

    private async Task ProcessScheduledNotificationsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

        var now = DateTime.UtcNow;
        var scheduledNotifications = await context.Notifications
            .Where(n => n.Status == NotificationStatus.Pending &&
                        n.ScheduledAt.HasValue &&
                        n.ScheduledAt <= now)
            .Take(100)
            .ToListAsync(cancellationToken);

        if (scheduledNotifications.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Found {Count} scheduled notifications ready for processing", scheduledNotifications.Count);

        foreach (var notification in scheduledNotifications)
        {
            notification.Status = NotificationStatus.Processing;
            await _notificationQueue.EnqueueAsync(notification, cancellationToken);

            _logger.LogDebug("Queued scheduled notification {Id} for processing", notification.Id);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
