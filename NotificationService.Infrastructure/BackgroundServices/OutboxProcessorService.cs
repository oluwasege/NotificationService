using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Interfaces;
using NotificationService.Infrastructure.Data;
using System.Text.Json;

namespace NotificationService.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that processes outbox messages to ensure reliable message delivery.
/// Implements the transactional outbox pattern for guaranteed delivery.
/// </summary>
public class OutboxProcessorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly INotificationQueue _notificationQueue;
    private readonly ILogger<OutboxProcessorService> _logger;
    private const int BatchSize = 100;
    private const int MaxRetries = 5;

    public OutboxProcessorService(
        IServiceProvider serviceProvider,
        INotificationQueue notificationQueue,
        ILogger<OutboxProcessorService> logger)
    {
        _serviceProvider = serviceProvider;
        _notificationQueue = notificationQueue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox Processor Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox messages");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        _logger.LogInformation("Outbox Processor Service stopped");
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

        // Get unprocessed messages
        var messages = await context.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.ProcessingAttempts < MaxRetries)
            .OrderBy(m => m.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            return;
        }

        _logger.LogDebug("Processing {Count} outbox messages", messages.Count);

        foreach (var message in messages)
        {
            try
            {
                await ProcessMessageAsync(context, message, cancellationToken);
                message.ProcessedAt = DateTime.UtcNow;
                message.Error = null;
                _logger.LogDebug("Processed outbox message {Id}", message.Id);
            }
            catch (Exception ex)
            {
                message.ProcessingAttempts++;
                message.Error = ex.Message;
                _logger.LogWarning(ex, "Failed to process outbox message {Id}, attempt {Attempt}", 
                    message.Id, message.ProcessingAttempts);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task ProcessMessageAsync(
        NotificationDbContext context, 
        OutboxMessage message, 
        CancellationToken cancellationToken)
    {
        switch (message.MessageType)
        {
            case "Notification":
                await ProcessNotificationMessageAsync(context, message, cancellationToken);
                break;
            case "Webhook":
                // Webhook processing would be handled by WebhookService
                _logger.LogDebug("Webhook message {Id} skipped - handled by WebhookService", message.Id);
                break;
            default:
                _logger.LogWarning("Unknown message type: {Type}", message.MessageType);
                break;
        }
    }

    private async Task ProcessNotificationMessageAsync(
        NotificationDbContext context,
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<NotificationOutboxPayload>(message.Payload);
        if (payload == null)
        {
            throw new InvalidOperationException("Invalid notification payload");
        }

        var notification = await context.Notifications
            .FirstOrDefaultAsync(n => n.Id == payload.Id, cancellationToken);

        if (notification == null)
        {
            _logger.LogWarning("Notification {Id} not found for outbox message", payload.Id);
            return;
        }

        // Only enqueue if still in a processable state
        if (notification.Status == Domain.Enums.NotificationStatus.Processing ||
            notification.Status == Domain.Enums.NotificationStatus.Retrying)
        {
            await _notificationQueue.EnqueueAsync(notification, cancellationToken);
            _logger.LogDebug("Enqueued notification {Id} from outbox", notification.Id);
        }
    }

    public record NotificationOutboxPayload(Guid Id, string Type, string Priority);
}
