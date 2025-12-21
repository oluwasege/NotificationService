using System.ComponentModel;

namespace NotificationService.Domain.Enums;

public enum NotificationPriority
{
    [Description("Low Priority")]
    Low = 1,
    [Description("Normal Priority")]
    Normal,
    [Description("High Priority")]
    High,
    [Description("Critical Priority")]
    Critical
}
