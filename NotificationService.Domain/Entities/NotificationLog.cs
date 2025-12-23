using NotificationService.Domain.Enums;

namespace NotificationService.Domain.Entities;

public class NotificationLog : BaseEntity<Guid>
{
    public Guid NotificationId { get; set; }
    public NotificationStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Details { get; set; }
    public string ProviderResponse { get; set; }

    public virtual Notification Notification { get; set; } = null!;
}
