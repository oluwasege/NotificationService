using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Interfaces;
using NotificationService.Infrastructure.Data;
using NotificationService.Infrastructure.Providers;

namespace NotificationService.Infrastructure.BackgroundServices;

public class NotificationProcessorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly INotificationQueue _notificationQueue;
    private readonly ILogger<NotificationProcessorService> _logger;
    private const int MaxConcurrentProcessing = 10;

    public NotificationProcessorService(
        IServiceProvider serviceProvider,
        INotificationQueue notificationQueue,
        ILogger<NotificationProcessorService> logger)
    {
        _serviceProvider = serviceProvider;
        _notificationQueue = notificationQueue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notification Processor Service started");

        var tasks = new List<Task>();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Remove completed tasks
                tasks.RemoveAll(t => t.IsCompleted);

                // Process notifications up to max concurrent limit
                while (tasks.Count < MaxConcurrentProcessing)
                {
                    var notification = await _notificationQueue.DequeueAsync(stoppingToken);
                    if (notification == null)
                    {
                        break;
                    }

                    tasks.Add(ProcessNotificationAsync(notification, stoppingToken));
                }

                if (tasks.Count == 0)
                {
                    await Task.Delay(100, stoppingToken);
                }
                else
                {
                    await Task.WhenAny(tasks);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in notification processor loop");
                await Task.Delay(1000, stoppingToken);
            }
        }

        // Wait for remaining tasks to complete
        if (tasks.Count > 0)
        {
            _logger.LogInformation("Waiting for {Count} pending notifications to complete", tasks.Count);
            await Task.WhenAll(tasks);
        }

        _logger.LogInformation("Notification Processor Service stopped");
    }

    private async Task ProcessNotificationAsync(Notification notification, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var providerFactory = scope.ServiceProvider.GetRequiredService<NotificationProviderFactory>();

        try
        {
            _logger.LogInformation(
                "Processing notification {Id} of type {Type} to {Recipient}",
                notification.Id, notification.Type, notification.Recipient);

            // Get fresh notification from database
            var dbNotification = await context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notification.Id, cancellationToken);

            if (dbNotification == null)
            {
                _logger.LogWarning("Notification {Id} not found in database", notification.Id);
                return;
            }

            // Get appropriate provider
            var provider = providerFactory.GetProvider(dbNotification.Type);

            // Update status to processing
            dbNotification.Status = NotificationStatus.Processing;
            await AddLogAsync(context, dbNotification.Id, NotificationStatus.Processing,
                $"Processing with {provider.ProviderName}", cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            // Send notification
            var result = await provider.SendAsync(dbNotification, cancellationToken);

            if (result.Success)
            {
                dbNotification.Status = NotificationStatus.Sent;
                dbNotification.SentAt = DateTime.UtcNow;
                dbNotification.ExternalId = result.ExternalId;
                dbNotification.ErrorMessage = null;

                await AddLogAsync(context, dbNotification.Id, NotificationStatus.Sent,
                    result.Message ?? "Sent successfully", cancellationToken, result.ProviderResponse);

                _logger.LogInformation(
                    "Notification {Id} sent successfully. ExternalId: {ExternalId}",
                    notification.Id, result.ExternalId);

                // Simulate delivery confirmation (in production, this would be a webhook)
                _ = SimulateDeliveryConfirmationAsync(dbNotification.Id, cancellationToken);
            }
            else
            {
                await HandleFailureAsync(context, dbNotification, result.Message, result.ProviderResponse, cancellationToken);
            }

            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing notification {Id}", notification.Id);

            using var errorScope = _serviceProvider.CreateScope();
            var errorContext = errorScope.ServiceProvider.GetRequiredService<NotificationDbContext>();

            var dbNotification = await errorContext.Notifications
                .FirstOrDefaultAsync(n => n.Id == notification.Id, cancellationToken);

            if (dbNotification != null)
            {
                await HandleFailureAsync(errorContext, dbNotification, ex.Message, null, cancellationToken);
                await errorContext.SaveChangesAsync(cancellationToken);
            }
        }
    }

    private async Task HandleFailureAsync(
        NotificationDbContext context,
        Notification notification,
        string? errorMessage,
        string? providerResponse,
        CancellationToken cancellationToken)
    {
        notification.RetryCount++;
        notification.ErrorMessage = errorMessage;

        if (notification.RetryCount < notification.MaxRetries)
        {
            notification.Status = NotificationStatus.Retrying;

            await AddLogAsync(context, notification.Id, NotificationStatus.Retrying,
                $"Retry {notification.RetryCount}/{notification.MaxRetries}: {errorMessage}",
                cancellationToken, providerResponse);

            _logger.LogWarning(
                "Notification {Id} failed, scheduling retry {RetryCount}/{MaxRetries}",
                notification.Id, notification.RetryCount, notification.MaxRetries);

            // Re-queue with exponential backoff delay
            var delay = TimeSpan.FromSeconds(Math.Pow(2, notification.RetryCount) * 5);
            _ = Task.Run(async () =>
            {
                await Task.Delay(delay, cancellationToken);
                await _notificationQueue.EnqueueAsync(notification, cancellationToken);
            }, cancellationToken);
        }
        else
        {
            notification.Status = NotificationStatus.Failed;

            await AddLogAsync(context, notification.Id, NotificationStatus.Failed,
                $"Max retries exceeded: {errorMessage}", cancellationToken, providerResponse);

            _logger.LogError(
                "Notification {Id} failed permanently after {RetryCount} retries",
                notification.Id, notification.RetryCount);
        }
    }

    private async Task SimulateDeliveryConfirmationAsync(Guid notificationId, CancellationToken cancellationToken)
    {
        try
        {
            // Simulate delivery delay
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

            var notification = await context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken);

            if (notification != null && notification.Status == NotificationStatus.Sent)
            {
                notification.Status = NotificationStatus.Delivered;
                notification.DeliveredAt = DateTime.UtcNow;

                await AddLogAsync(context, notification.Id, NotificationStatus.Delivered,
                    "Delivery confirmed", cancellationToken);

                await context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Notification {Id} delivery confirmed", notificationId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error confirming delivery for notification {Id}", notificationId);
        }
    }

    private static async Task AddLogAsync(
        NotificationDbContext context,
        Guid notificationId,
        NotificationStatus status,
        string message,
        CancellationToken cancellationToken,
        string? providerResponse = null)
    {
        var log = new NotificationLog
        {
            NotificationId = notificationId,
            Status = status,
            Message = message,
            ProviderResponse = providerResponse
        };

        await context.NotificationLogs.AddAsync(log, cancellationToken);
    }
}
