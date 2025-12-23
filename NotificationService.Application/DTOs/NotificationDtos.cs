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
    string? CorrelationId = null,
    string? IdempotencyKey = null,
    Guid? TemplateId = null,
    Dictionary<string, object>? TemplateData = null
);

public record SendNotificationResponse(
    Guid NotificationId,
    NotificationStatus Status,
    string Message,
    DateTime CreatedAt,
    bool WasIdempotent = false
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

// Template DTOs
public record CreateTemplateRequest(
    string Name,
    string Description,
    NotificationType Type,
    string SubjectTemplate,
    string BodyTemplate
);

public record UpdateTemplateRequest(
    string? Name = null,
    string? Description = null,
    string? SubjectTemplate = null,
    string? BodyTemplate = null,
    bool? IsActive = null
);

public record TemplateDto(
    Guid Id,
    string Name,
    string Description,
    NotificationType Type,
    string SubjectTemplate,
    string BodyTemplate,
    bool IsActive,
    DateTime CreatedAt
);

// Webhook DTOs
public record CreateWebhookRequest(
    string Name,
    string Url,
    string Events,
    string? Secret = null
);

public record UpdateWebhookRequest(
    string? Name = null,
    string? Url = null,
    string? Events = null,
    bool? IsActive = null
);

public record WebhookDto(
    Guid Id,
    string Name,
    string Url,
    string Events,
    bool IsActive,
    int FailureCount,
    DateTime? LastSuccessAt,
    DateTime? LastFailureAt,
    DateTime CreatedAt
);

public record WebhookEventPayload(
    Guid NotificationId,
    NotificationStatus Status,
    NotificationType Type,
    string Recipient,
    DateTime Timestamp,
    string? ErrorMessage = null,
    string? ExternalId = null
);
