using System.ComponentModel;

namespace NotificationService.Domain.Enums;

public enum NotificationStatus
{
    [Description("Pending")]
    Pending = 1,
    [Description("Processing")]
    Processing,
    [Description("Sent")]
    Sent,
    [Description("Delivered")]
    Delivered,
    [Description("Failed")]
    Failed,
    [Description("Retrying")]
    Retrying
}
