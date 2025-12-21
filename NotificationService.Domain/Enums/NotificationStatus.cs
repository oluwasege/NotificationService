namespace NotificationService.Domain.Enums;

public enum NotificationStatus
{
    Pending = 1,
    Processing = 2,
    Sent = 3,
    Delivered = 4,
    Failed = 5,
    Retrying = 6
}
