using NotificationService.Application.DTOs;

namespace NotificationService.Application.Interfaces;

public interface INotificationService
{
    Task<SendNotificationResponse> SendNotificationAsync(
        Guid userId,
        Guid subscriptionId,
        SendNotificationRequest request,
        CancellationToken cancellationToken = default);

    Task<SendBatchNotificationResponse> SendBatchNotificationsAsync(
        Guid userId,
        Guid subscriptionId,
        SendBatchNotificationRequest request,
        CancellationToken cancellationToken = default);

    Task<NotificationDetailDto?> GetNotificationByIdAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<NotificationDto>> GetNotificationsAsync(
        Guid? userId,
        NotificationQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<bool> CancelNotificationAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default);

    Task<bool> RetryNotificationAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default);
}
