using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;

namespace NotificationService.Domain.Interfaces;

public interface INotificationQueue
{
    ValueTask EnqueueAsync(Notification notification, CancellationToken cancellationToken = default);
    ValueTask<Notification> DequeueAsync(CancellationToken cancellationToken = default);
    int GetQueueCount();
    int GetQueueDepth(NotificationPriority priority);
}
