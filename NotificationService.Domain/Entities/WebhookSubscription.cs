namespace NotificationService.Domain.Entities;

/// <summary>
/// Represents a webhook subscription for receiving notification status updates.
/// </summary>
public class WebhookSubscription : BaseEntity<Guid>
{
    /// <summary>
    /// The subscription this webhook belongs to
    /// </summary>
    public Guid SubscriptionId { get; set; }

    /// <summary>
    /// Friendly name for the webhook
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The URL to POST webhook events to
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Secret key for HMAC signature verification
    /// </summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// Comma-separated list of events to subscribe to (e.g., "Sent,Delivered,Failed")
    /// </summary>
    public string Events { get; set; } = string.Empty;

    /// <summary>
    /// Whether the webhook is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Number of consecutive failures
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// Last successful delivery timestamp
    /// </summary>
    public DateTime? LastSuccessAt { get; set; }

    /// <summary>
    /// Last failure timestamp
    /// </summary>
    public DateTime? LastFailureAt { get; set; }

    /// <summary>
    /// Navigation property to the parent subscription
    /// </summary>
    public virtual Subscription Subscription { get; set; } = null!;
}
