using System.ComponentModel;

namespace NotificationService.Domain.Enums;

public enum NotificationType
{
    [Description("Email Notification")]
    Email = 1,
    [Description("SMS Notification")]
    Sms
}
