using NotificationService.Domain.Enums;

namespace NotificationService.Domain.Entities;

/// <summary>
/// Represents a message in the outbox for reliable message processing.
/// Used to ensure transactional consistency between database and message queue.
/// </summary>
public class OutboxMessage : BaseEntity<Guid>
{
    /// <summary>
    /// The type of message (e.g., "Notification", "Webhook")
    /// </summary>
    public string MessageType { get; set; } = string.Empty;

    /// <summary>
    /// The aggregate/entity ID this message relates to
    /// </summary>
    public Guid AggregateId { get; set; }

    /// <summary>
    /// JSON serialized payload of the message
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// When the message was processed (null if not yet processed)
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// Number of processing attempts
    /// </summary>
    public int ProcessingAttempts { get; set; }

    /// <summary>
    /// Error message if processing failed
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// Whether the message has been successfully processed
    /// </summary>
    public bool IsProcessed => ProcessedAt.HasValue;
}
