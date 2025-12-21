using System.ComponentModel;

namespace NotificationService.Domain.Enums;

public enum SubscriptionStatus
{
    [Description("Active")]
    Active = 1,
    [Description("Suspended")]
    Suspended,
    [Description("Expired")]
    Expired,
    [Description("Revoked")]
    Revoked
}
