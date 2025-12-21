namespace NotificationService.Application.DTOs;

public record LoginRequest(
    string Email,
    string Password
);

public record LoginResponse(
    string Token,
    string TokenType,
    int ExpiresIn,
    UserDto User
);

public record RefreshTokenRequest(
    string Token
);

public record SubscriptionKeyValidationResult(
    bool IsValid,
    Guid? UserId,
    Guid? SubscriptionId,
    string? ErrorMessage,
    bool? AllowSms,
    bool? AllowEmail,
    int? RemainingDailyQuota,
    int? RemainingMonthlyQuota
);
