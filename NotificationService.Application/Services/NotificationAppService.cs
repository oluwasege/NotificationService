using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Interfaces;

namespace NotificationService.Application.Services;

public class NotificationAppService : INotificationService
{
    private readonly IRepository<Notification> _notificationRepository;
    private readonly IRepository<NotificationLog> _logRepository;
    private readonly INotificationQueue _notificationQueue;
    private readonly ISubscriptionValidationService _subscriptionValidation;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<NotificationAppService> _logger;

    public NotificationAppService(
        IRepository<Notification> notificationRepository,
        IRepository<NotificationLog> logRepository,
        INotificationQueue notificationQueue,
        ISubscriptionValidationService subscriptionValidation,
        IUnitOfWork unitOfWork,
        ILogger<NotificationAppService> logger)
    {
        _notificationRepository = notificationRepository;
        _logRepository = logRepository;
        _notificationQueue = notificationQueue;
        _subscriptionValidation = subscriptionValidation;
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

        if (!await _subscriptionValidation.CanSendNotificationAsync(subscriptionId, request.Type, cancellationToken))
        {
            _logger.LogWarning("Notification type {Type} not allowed for subscription {SubscriptionId}", request.Type, subscriptionId);
            throw new InvalidOperationException($"Notification type {request.Type} is not allowed for this subscription");
        }

        var notification = new Notification
        {
            UserId = userId,
            SubscriptionId = subscriptionId,
            Type = request.Type,
            Priority = request.Priority,
            Recipient = request.Recipient,
            Subject = request.Subject,
            Body = request.Body,
            Metadata = request.Metadata,
            CorrelationId = request.CorrelationId ?? Guid.NewGuid().ToString("N"),
            ScheduledAt = request.ScheduledAt,
            Status = request.ScheduledAt.HasValue && request.ScheduledAt > DateTime.UtcNow
                ? NotificationStatus.Pending
                : NotificationStatus.Processing
        };

        await _notificationRepository.AddAsync(notification, cancellationToken);

        var log = new NotificationLog
        {
            NotificationId = notification.Id,
            Status = notification.Status,
            Message = "Notification created and queued for processing"
        };
        await _logRepository.AddAsync(log, cancellationToken);

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
        var notification = await _notificationRepository
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
            notification.Logs.Select(l => new NotificationLogDto(
                l.Id,
                l.Status,
                l.Message,
                l.Details,
                l.CreatedAt
            )).ToList()
        );
    }

    public async Task<PagedResult<NotificationDto>> GetNotificationsAsync(
        Guid? userId,
        NotificationQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var queryable = _notificationRepository.QueryNoTracking();

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
        var notification = await _notificationRepository.GetByIdAsync(notificationId, cancellationToken);
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

        await _notificationRepository.UpdateAsync(notification, cancellationToken);
        await _logRepository.AddAsync(log, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Notification {Id} cancelled", notificationId);
        return true;
    }

    public async Task<bool> RetryNotificationAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _notificationRepository.GetByIdAsync(notificationId, cancellationToken);
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

        await _notificationRepository.UpdateAsync(notification, cancellationToken);
        await _logRepository.AddAsync(log, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _notificationQueue.EnqueueAsync(notification, cancellationToken);

        _logger.LogInformation("Notification {Id} queued for retry", notificationId);
        return true;
    }
}
