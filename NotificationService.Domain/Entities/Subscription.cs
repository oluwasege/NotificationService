using NotificationService.Domain.Enums;

namespace NotificationService.Domain.Entities;

public class Subscription : BaseEntity
{
    public Guid UserId { get; set; }
    public string SubscriptionKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public DateTime ExpiresAt { get; set; }
    public int DailyLimit { get; set; } = 1000;
    public int MonthlyLimit { get; set; } = 30000;
    public int DailyUsed { get; set; }
    public int MonthlyUsed { get; set; }
    public DateTime LastResetDaily { get; set; } = DateTime.UtcNow.Date;
    public DateTime LastResetMonthly { get; set; } = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
    public bool AllowSms { get; set; } = true;
    public bool AllowEmail { get; set; } = true;

    public virtual User User { get; set; } = null!;
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
