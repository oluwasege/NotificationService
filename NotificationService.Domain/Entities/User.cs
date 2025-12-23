using NotificationService.Domain.Enums;

namespace NotificationService.Domain.Entities;

public class User : BaseEntity<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.User;
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }

    public virtual ICollection<Subscription> Subscriptions { get; set; } = [];
    public virtual ICollection<Notification> Notifications { get; set; } = [];
}
