using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Interfaces;

namespace NotificationService.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<Subscription> _subscriptionRepository;
    private readonly IRepository<Notification> _notificationRepository;
    private readonly INotificationQueue _notificationQueue;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(
        IRepository<User> userRepository,
        IRepository<Subscription> subscriptionRepository,
        IRepository<Notification> notificationRepository,
        INotificationQueue notificationQueue,
        ILogger<DashboardService> logger)
    {
        _userRepository = userRepository;
        _subscriptionRepository = subscriptionRepository;
        _notificationRepository = notificationRepository;
        _notificationQueue = notificationQueue;
        _logger = logger;
    }

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Generating dashboard summary");

        var totalUsers = await _userRepository.CountAsync(cancellationToken: cancellationToken);
        var activeUsers = await _userRepository.CountAsync(u => u.IsActive, cancellationToken);

        var totalSubscriptions = await _subscriptionRepository.CountAsync(cancellationToken: cancellationToken);
        var activeSubscriptions = await _subscriptionRepository.CountAsync(
            s => s.Status == SubscriptionStatus.Active, cancellationToken);

        var today = DateTime.UtcNow.Date;
        var notificationsSummary = await GetNotificationsSummaryAsync(today, cancellationToken);
        var last7DaysStats = await GetNotificationStatsAsync(today.AddDays(-6), today, cancellationToken);

        var systemHealth = GetSystemHealth(notificationsSummary);

        return new DashboardSummaryDto(
            totalUsers,
            activeUsers,
            totalSubscriptions,
            activeSubscriptions,
            notificationsSummary,
            last7DaysStats,
            systemHealth
        );
    }

    public async Task<List<UserStatsDto>> GetTopUsersAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        var users = await _userRepository
            .QueryNoTracking()
            .Include(u => u.Subscriptions)
            .Include(u => u.Notifications)
            .OrderByDescending(u => u.Notifications.Count)
            .Take(count)
            .Select(u => new UserStatsDto(
                u.Id,
                u.Name,
                u.Notifications.Count,
                u.Subscriptions.Count,
                u.Subscriptions.Count(s => s.Status == SubscriptionStatus.Active)
            ))
            .ToListAsync(cancellationToken);

        return users;
    }

    public async Task<List<DailyNotificationStatsDto>> GetNotificationStatsAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        var stats = await _notificationRepository
            .QueryNoTracking()
            .Where(n => n.CreatedAt.Date >= fromDate.Date && n.CreatedAt.Date <= toDate.Date)
            .GroupBy(n => n.CreatedAt.Date)
            .Select(g => new DailyNotificationStatsDto(
                g.Key,
                g.Count(),
                g.Count(n => n.Status == NotificationStatus.Sent || n.Status == NotificationStatus.Delivered),
                g.Count(n => n.Status == NotificationStatus.Failed),
                g.Count(n => n.Type == NotificationType.Email),
                g.Count(n => n.Type == NotificationType.Sms)
            ))
            .OrderBy(s => s.Date)
            .ToListAsync(cancellationToken);

        // Fill in missing dates with zero counts
        var allDates = Enumerable.Range(0, (toDate.Date - fromDate.Date).Days + 1)
            .Select(d => fromDate.Date.AddDays(d))
            .ToList();

        var result = allDates.Select(date =>
            stats.FirstOrDefault(s => s.Date == date) ??
            new DailyNotificationStatsDto(date, 0, 0, 0, 0, 0)
        ).ToList();

        return result;
    }

    private async Task<NotificationSummaryDto> GetNotificationsSummaryAsync(
        DateTime today,
        CancellationToken cancellationToken)
    {
        var notifications = _notificationRepository.QueryNoTracking();

        var totalNotifications = await notifications.CountAsync(cancellationToken);
        var pendingNotifications = await notifications.CountAsync(n => n.Status == NotificationStatus.Pending, cancellationToken);
        var processingNotifications = await notifications.CountAsync(n => n.Status == NotificationStatus.Processing, cancellationToken);
        var sentNotifications = await notifications.CountAsync(n => n.Status == NotificationStatus.Sent, cancellationToken);
        var deliveredNotifications = await notifications.CountAsync(n => n.Status == NotificationStatus.Delivered, cancellationToken);
        var failedNotifications = await notifications.CountAsync(n => n.Status == NotificationStatus.Failed, cancellationToken);

        var todayNotifications = notifications.Where(n => n.CreatedAt.Date == today);
        var todayTotal = await todayNotifications.CountAsync(cancellationToken);
        var todaySent = await todayNotifications.CountAsync(
            n => n.Status == NotificationStatus.Sent || n.Status == NotificationStatus.Delivered, cancellationToken);
        var todayFailed = await todayNotifications.CountAsync(n => n.Status == NotificationStatus.Failed, cancellationToken);

        return new NotificationSummaryDto(
            totalNotifications,
            pendingNotifications,
            processingNotifications,
            sentNotifications,
            deliveredNotifications,
            failedNotifications,
            todayTotal,
            todaySent,
            todayFailed
        );
    }

    private SystemHealthDto GetSystemHealth(NotificationSummaryDto summary)
    {
        var queueSize = _notificationQueue.GetQueueCount();
        var totalProcessed = summary.SentNotifications + summary.DeliveredNotifications + summary.FailedNotifications;
        var successRate = totalProcessed > 0
            ? (double)(summary.SentNotifications + summary.DeliveredNotifications) / totalProcessed * 100
            : 100.0;

        var status = queueSize switch
        {
            < 100 => "Healthy",
            < 1000 => "Moderate",
            _ => "High Load"
        };

        if (successRate < 90) status = "Degraded";
        if (successRate < 80) status = "Critical";

        return new SystemHealthDto(
            queueSize,
            150.0, // Mock average processing time
            Math.Round(successRate, 2),
            status
        );
    }
}
