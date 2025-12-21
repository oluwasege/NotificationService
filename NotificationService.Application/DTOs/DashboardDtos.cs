namespace NotificationService.Application.DTOs;

public record DashboardSummaryDto(
    int TotalUsers,
    int ActiveUsers,
    int TotalSubscriptions,
    int ActiveSubscriptions,
    NotificationSummaryDto NotificationsSummary,
    List<DailyNotificationStatsDto> Last7DaysStats,
    SystemHealthDto SystemHealth
);

public record NotificationSummaryDto(
    int TotalNotifications,
    int PendingNotifications,
    int ProcessingNotifications,
    int SentNotifications,
    int DeliveredNotifications,
    int FailedNotifications,
    int TodayTotal,
    int TodaySent,
    int TodayFailed
);

public record DailyNotificationStatsDto(
    DateTime Date,
    int Total,
    int Sent,
    int Failed,
    int EmailCount,
    int SmsCount
);

public record SystemHealthDto(
    int QueueSize,
    double AverageProcessingTimeMs,
    double SuccessRate,
    string Status
);

public record UserStatsDto(
    Guid UserId,
    string UserName,
    int TotalNotifications,
    int TotalSubscriptions,
    int ActiveSubscriptions
);
