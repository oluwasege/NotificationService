using NotificationService.Domain.Enums;

namespace NotificationService.Application.DTOs;

public record NotificationDto(
    Guid Id,
    NotificationType Type,
    NotificationStatus Status,
    NotificationPriority Priority,
    string Recipient,
    string Subject,
    string Body,
    int RetryCount,
    DateTime CreatedAt,
    DateTime? ScheduledAt,
    DateTime? SentAt,
    DateTime? DeliveredAt,
    string? ErrorMessage,
    string? CorrelationId
);

public record NotificationDetailDto(
    Guid Id,
    NotificationType Type,
    NotificationStatus Status,
    NotificationPriority Priority,
    string Recipient,
    string Subject,
    string Body,
    string? Metadata,
    int RetryCount,
    int MaxRetries,
    DateTime CreatedAt,
    DateTime? ScheduledAt,
    DateTime? SentAt,
    DateTime? DeliveredAt,
    string? ErrorMessage,
    string? ExternalId,
    string? CorrelationId,
    Guid UserId,
    Guid SubscriptionId,
    List<NotificationLogDto> Logs
);

public record NotificationLogDto(
    Guid Id,
    NotificationStatus Status,
    string Message,
    string? Details,
    DateTime CreatedAt
);

public record NotificationQueryRequest(
    NotificationType? Type = null,
    NotificationStatus? Status = null,
    NotificationPriority? Priority = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? Recipient = null,
    string? CorrelationId = null,
    int Page = 1,
    int PageSize = 20
);

public record PagedResult<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);
