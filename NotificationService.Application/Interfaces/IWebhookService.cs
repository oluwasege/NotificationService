using NotificationService.Application.DTOs;
using NotificationService.Domain.Enums;

namespace NotificationService.Application.Interfaces;

/// <summary>
/// Service for managing webhook subscriptions and delivering webhook events.
/// </summary>
public interface IWebhookService
{
    /// <summary>
    /// Sends a webhook event to all registered webhooks for a subscription.
    /// </summary>
    Task SendWebhookAsync(
        Guid subscriptionId,
        WebhookEventPayload payload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new webhook subscription.
    /// </summary>
    Task<WebhookDto> CreateWebhookAsync(
        Guid subscriptionId,
        CreateWebhookRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all webhooks for a subscription.
    /// </summary>
    Task<List<WebhookDto>> GetWebhooksAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a webhook by ID.
    /// </summary>
    Task<WebhookDto?> GetWebhookByIdAsync(
        Guid webhookId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a webhook.
    /// </summary>
    Task<WebhookDto?> UpdateWebhookAsync(
        Guid webhookId,
        UpdateWebhookRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a webhook.
    /// </summary>
    Task<bool> DeleteWebhookAsync(
        Guid webhookId,
        CancellationToken cancellationToken = default);
}
