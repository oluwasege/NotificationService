using NotificationService.Domain.Enums;

namespace NotificationService.Application.DTOs;

public record SubscriptionDto(
    Guid Id,
    string Name,
    string SubscriptionKey,
    SubscriptionStatus Status,
    DateTime ExpiresAt,
    int DailyLimit,
    int MonthlyLimit,
    int DailyUsed,
    int MonthlyUsed,
    bool AllowSms,
    bool AllowEmail,
    DateTime CreatedAt
);

public record CreateSubscriptionRequest(
    Guid UserId,
    string Name,
    int DailyLimit = 1000,
    int MonthlyLimit = 30000,
    int ExpiresInDays = 365,
    bool AllowSms = true,
    bool AllowEmail = true
);

public record UpdateSubscriptionRequest(
    string? Name = null,
    SubscriptionStatus? Status = null,
    int? DailyLimit = null,
    int? MonthlyLimit = null,
    DateTime? ExpiresAt = null,
    bool? AllowSms = null,
    bool? AllowEmail = null
);

public record RegenerateKeyResponse(
    string NewSubscriptionKey,
    DateTime GeneratedAt
);
