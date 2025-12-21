using NotificationService.Domain.Enums;

namespace NotificationService.Application.DTOs;

public record SendNotificationRequest(
    NotificationType Type,
    string Recipient,
    string Subject,
    string Body,
    NotificationPriority Priority = NotificationPriority.Normal,
    DateTime? ScheduledAt = null,
    string? Metadata = null,
    string? CorrelationId = null
);

public record SendNotificationResponse(
    Guid NotificationId,
    NotificationStatus Status,
    string Message,
    DateTime CreatedAt
);

public record SendBatchNotificationRequest(
    List<SendNotificationRequest> Notifications
);

public record SendBatchNotificationResponse(
    int TotalCount,
    int AcceptedCount,
    int RejectedCount,
    List<BatchNotificationResult> Results
);

public record BatchNotificationResult(
    int Index,
    Guid? NotificationId,
    bool Accepted,
    string? ErrorMessage
);
