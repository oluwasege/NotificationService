using NotificationService.Domain.Enums;

namespace NotificationService.Domain.Entities;

public class Notification : BaseEntity<Guid>
{
    public Guid UserId { get; set; }
    public Guid SubscriptionId { get; set; }
    public NotificationType Type { get; set; }
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
    public string Recipient { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Metadata { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; } = 3;
    public DateTime? ScheduledAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string ErrorMessage { get; set; }
    public string ExternalId { get; set; }
    public string CorrelationId { get; set; }

    public virtual User User { get; set; } = null!;
    public virtual Subscription Subscription { get; set; } = null!;
    public virtual ICollection<NotificationLog> Logs { get; set; } = [];
}
