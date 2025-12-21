using NotificationService.Application.DTOs;

namespace NotificationService.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken cancellationToken = default);
    Task<List<UserStatsDto>> GetTopUsersAsync(int count = 10, CancellationToken cancellationToken = default);
    Task<List<DailyNotificationStatsDto>> GetNotificationStatsAsync(
        DateTime fromDate, 
        DateTime toDate, 
        CancellationToken cancellationToken = default);
}
