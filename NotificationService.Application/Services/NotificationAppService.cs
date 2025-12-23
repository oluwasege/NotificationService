using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Interfaces;
using System.Text.Json;

namespace NotificationService.Application.Services;

public class NotificationAppService : INotificationService
{
    private readonly INotificationQueue _notificationQueue;
    private readonly ISubscriptionValidationService _subscriptionValidation;
    private readonly ITemplateService _templateService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<NotificationAppService> _logger;

    public NotificationAppService(
        INotificationQueue notificationQueue,
        ISubscriptionValidationService subscriptionValidation,
        ITemplateService templateService,
        IUnitOfWork unitOfWork,
        ILogger<NotificationAppService> logger)
    {
        _notificationQueue = notificationQueue;
        _subscriptionValidation = subscriptionValidation;
        _templateService = templateService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<SendNotificationResponse> SendNotificationAsync(
        Guid userId,
        Guid subscriptionId,
        SendNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating notification for user {UserId}, type: {Type}, recipient: {Recipient}",
            userId, request.Type, request.Recipient);

        // Check idempotency - return existing notification if already processed
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existingNotification = await _unitOfWork.GetRepository<Notification>()
                .GetAllQueryable()
                .FirstOrDefaultAsync(n => n.IdempotencyKey == request.IdempotencyKey, cancellationToken);

            if (existingNotification != null)
            {
                _logger.LogInformation(
                    "Idempotent request detected for key {IdempotencyKey}, returning existing notification {Id}",
                    request.IdempotencyKey, existingNotification.Id);

                return new SendNotificationResponse(
                    existingNotification.Id,
                    existingNotification.Status,
                    "Notification already exists (idempotent request)",
                    existingNotification.CreatedAt,
                    WasIdempotent: true
                );
            }
        }

        if (!await _subscriptionValidation.CanSendNotificationAsync(subscriptionId, request.Type, cancellationToken))
        {
            _logger.LogWarning("Notification type {Type} not allowed for subscription {SubscriptionId}", request.Type, subscriptionId);
            throw new InvalidOperationException($"Notification type {request.Type} is not allowed for this subscription");
        }

        // Render template if provided
        var subject = request.Subject;
        var body = request.Body;
        if (request.TemplateId.HasValue)
        {
            var rendered = await _templateService.RenderTemplateAsync(
                request.TemplateId.Value,
                request.TemplateData,
                cancellationToken);

            if (rendered == null)
            {
                throw new InvalidOperationException($"Template {request.TemplateId} not found");
            }

            subject = rendered.Value.Subject;
            body = rendered.Value.Body;
            _logger.LogDebug("Rendered template {TemplateId} for notification", request.TemplateId);
        }

        var notification = new Notification
        {
            UserId = userId,
            SubscriptionId = subscriptionId,
            Type = request.Type,
            Priority = request.Priority,
            Recipient = request.Recipient,
            Subject = subject,
            Body = body,
            Metadata = request.Metadata,
            CorrelationId = request.CorrelationId ?? Guid.NewGuid().ToString("N"),
            IdempotencyKey = request.IdempotencyKey,
            TemplateId = request.TemplateId,
            ScheduledAt = request.ScheduledAt,
            Status = request.ScheduledAt.HasValue && request.ScheduledAt > DateTime.UtcNow
                ? NotificationStatus.Pending
                : NotificationStatus.Processing
        };

        await _unitOfWork.GetRepository<Notification>().AddAsync(notification, cancellationToken);

        var log = new NotificationLog
        {
            NotificationId = notification.Id,
            Status = notification.Status,
            Message = "Notification created and queued for processing"
        };
        await _unitOfWork.GetRepository<NotificationLog>().AddAsync(log, cancellationToken);

        // Create outbox message for reliable processing
        var outboxMessage = new OutboxMessage
        {
            MessageType = "Notification",
            AggregateId = notification.Id,
            Payload = JsonSerializer.Serialize(new { notification.Id, notification.Type, notification.Priority })
        };
        await _unitOfWork.GetRepository<OutboxMessage>().AddAsync(outboxMessage, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _subscriptionValidation.IncrementUsageAsync(subscriptionId, cancellationToken);

        // Queue for immediate processing if not scheduled
        if (!request.ScheduledAt.HasValue || request.ScheduledAt <= DateTime.UtcNow)
        {
            await _notificationQueue.EnqueueAsync(notification, cancellationToken);
            _logger.LogInformation("Notification {Id} queued for immediate processing", notification.Id);
        }
        else
        {
            _logger.LogInformation("Notification {Id} scheduled for {ScheduledAt}", notification.Id, request.ScheduledAt);
        }

        return new SendNotificationResponse(
            notification.Id,
            notification.Status,
            "Notification created successfully",
            notification.CreatedAt
        );
    }

    public async Task<SendBatchNotificationResponse> SendBatchNotificationsAsync(
        Guid userId,
        Guid subscriptionId,
        SendBatchNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing batch of {Count} notifications for user {UserId}", request.Notifications.Count, userId);

        var results = new List<BatchNotificationResult>();
        var acceptedCount = 0;
        var rejectedCount = 0;

        for (var i = 0; i < request.Notifications.Count; i++)
        {
            try
            {
                var response = await SendNotificationAsync(userId, subscriptionId, request.Notifications[i], cancellationToken);
                results.Add(new BatchNotificationResult(i, response.NotificationId, true, null));
                acceptedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create notification at index {Index}", i);
                results.Add(new BatchNotificationResult(i, null, false, ex.Message));
                rejectedCount++;
            }
        }

        return new SendBatchNotificationResponse(
            request.Notifications.Count,
            acceptedCount,
            rejectedCount,
            results
        );
    }

    public async Task<NotificationDetailDto?> GetNotificationByIdAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        var notification = await _unitOfWork.GetRepository<Notification>()
            .QueryNoTracking()
            .Include(n => n.Logs.OrderByDescending(l => l.CreatedAt))
            .FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken);

        if (notification == null) return null;

        return new NotificationDetailDto(
            notification.Id,
            notification.Type,
            notification.Status,
            notification.Priority,
            notification.Recipient,
            notification.Subject,
            notification.Body,
            notification.Metadata,
            notification.RetryCount,
            notification.MaxRetries,
            notification.CreatedAt,
            notification.ScheduledAt,
            notification.SentAt,
            notification.DeliveredAt,
            notification.ErrorMessage,
            notification.ExternalId,
            notification.CorrelationId,
            notification.UserId,
            notification.SubscriptionId,
            [.. notification.Logs.Select(l => new NotificationLogDto(
                l.Id,
                l.Status,
                l.Message,
                l.Details,
                l.CreatedAt
            ))]
        );
    }

    public async Task<PagedResult<NotificationDto>> GetNotificationsAsync(
        Guid? userId,
        NotificationQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var queryable = _unitOfWork.GetRepository<Notification>().QueryNoTracking();

        if (userId.HasValue)
            queryable = queryable.Where(n => n.UserId == userId.Value);

        if (query.Type.HasValue)
            queryable = queryable.Where(n => n.Type == query.Type.Value);

        if (query.Status.HasValue)
            queryable = queryable.Where(n => n.Status == query.Status.Value);

        if (query.Priority.HasValue)
            queryable = queryable.Where(n => n.Priority == query.Priority.Value);

        if (query.FromDate.HasValue)
            queryable = queryable.Where(n => n.CreatedAt >= query.FromDate.Value);

        if (query.ToDate.HasValue)
            queryable = queryable.Where(n => n.CreatedAt <= query.ToDate.Value);

        if (!string.IsNullOrWhiteSpace(query.Recipient))
            queryable = queryable.Where(n => n.Recipient.Contains(query.Recipient));

        if (!string.IsNullOrWhiteSpace(query.CorrelationId))
            queryable = queryable.Where(n => n.CorrelationId == query.CorrelationId);

        var totalCount = await queryable.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);

        var items = await queryable
            .OrderByDescending(n => n.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(n => new NotificationDto(
                n.Id,
                n.Type,
                n.Status,
                n.Priority,
                n.Recipient,
                n.Subject,
                n.Body,
                n.RetryCount,
                n.CreatedAt,
                n.ScheduledAt,
                n.SentAt,
                n.DeliveredAt,
                n.ErrorMessage,
                n.CorrelationId
            ))
            .ToListAsync(cancellationToken);

        return new PagedResult<NotificationDto>(items, totalCount, query.Page, query.PageSize, totalPages);
    }

    public async Task<bool> CancelNotificationAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _unitOfWork.GetRepository<Notification>().GetByIdAsync(notificationId, cancellationToken);
        if (notification == null) return false;

        if (notification.Status != NotificationStatus.Pending)
        {
            _logger.LogWarning("Cannot cancel notification {Id} with status {Status}", notificationId, notification.Status);
            return false;
        }

        notification.Status = NotificationStatus.Failed;
        notification.ErrorMessage = "Cancelled by user";

        var log = new NotificationLog
        {
            NotificationId = notification.Id,
            Status = NotificationStatus.Failed,
            Message = "Notification cancelled by user"
        };

        await _unitOfWork.GetRepository<Notification>().UpdateAsync(notification, cancellationToken);
        await _unitOfWork.GetRepository<NotificationLog>().AddAsync(log, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Notification {Id} cancelled", notificationId);
        return true;
    }

    public async Task<bool> RetryNotificationAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _unitOfWork.GetRepository<Notification>().GetByIdAsync(notificationId, cancellationToken);
        if (notification == null) return false;

        if (notification.Status != NotificationStatus.Failed)
        {
            _logger.LogWarning("Cannot retry notification {Id} with status {Status}", notificationId, notification.Status);
            return false;
        }

        notification.Status = NotificationStatus.Retrying;
        notification.RetryCount++;
        notification.ErrorMessage = null;

        var log = new NotificationLog
        {
            NotificationId = notification.Id,
            Status = NotificationStatus.Retrying,
            Message = $"Manual retry initiated. Attempt {notification.RetryCount}"
        };

        await _unitOfWork.GetRepository<Notification>().UpdateAsync(notification, cancellationToken);
        await _unitOfWork.GetRepository<NotificationLog>().AddAsync(log, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _notificationQueue.EnqueueAsync(notification, cancellationToken);

        _logger.LogInformation("Notification {Id} queued for retry", notificationId);
        return true;
    }
}
