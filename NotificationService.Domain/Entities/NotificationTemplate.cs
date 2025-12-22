using NotificationService.Domain.Enums;

namespace NotificationService.Domain.Entities;

/// <summary>
/// Represents a reusable notification template with variable substitution support.
/// </summary>
public class NotificationTemplate : BaseEntity<Guid>
{
    /// <summary>
    /// Unique name/identifier for the template
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the template's purpose
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The type of notification this template is for
    /// </summary>
    public NotificationType Type { get; set; }

    /// <summary>
    /// Template for the subject line (supports Scriban syntax)
    /// </summary>
    public string SubjectTemplate { get; set; } = string.Empty;

    /// <summary>
    /// Template for the body content (supports Scriban syntax)
    /// </summary>
    public string BodyTemplate { get; set; } = string.Empty;

    /// <summary>
    /// Whether the template is active and can be used
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// The subscription that owns this template (null for system templates)
    /// </summary>
    public Guid? SubscriptionId { get; set; }

    /// <summary>
    /// Navigation property to the subscription
    /// </summary>
    public virtual Subscription Subscription { get; set; }

    /// <summary>
    /// Notifications using this template
    /// </summary>
    public virtual ICollection<Notification> Notifications { get; set; } = [];
}
