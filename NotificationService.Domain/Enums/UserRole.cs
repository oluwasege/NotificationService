using System.ComponentModel;

namespace NotificationService.Domain.Enums;

public enum UserRole
{
    [Description("Regular User")]
    User = 1,
    [Description("Administrator")]
    Admin,
    [Description("Super Administrator")]
    SuperAdmin
}
