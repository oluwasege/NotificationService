using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Interfaces;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NotificationService.Application.Services;

/// <summary>
/// Service for managing webhook subscriptions and delivering webhook events.
/// </summary>
public class WebhookService : IWebhookService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly HttpClient _httpClient;
    private readonly ILogger<WebhookService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public WebhookService(
        IUnitOfWork unitOfWork,
        ILogger<WebhookService> logger,
        HttpClient? httpClient = null)
    {
        _unitOfWork = unitOfWork;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _logger = logger;
    }

    public async Task SendWebhookAsync(
        Guid subscriptionId,
        WebhookEventPayload payload,
        CancellationToken cancellationToken = default)
    {
        var webhooks = await _unitOfWork.GetRepository<WebhookSubscription>()
            .QueryNoTracking()
            .Where(w => w.SubscriptionId == subscriptionId && w.IsActive)
            .ToListAsync(cancellationToken);

        if (webhooks.Count == 0)
        {
            return;
        }

        var eventName = payload.Status.ToString();
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);

        foreach (var webhook in webhooks)
        {
            // Check if webhook is subscribed to this event
            var subscribedEvents = webhook.Events.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (!subscribedEvents.Contains(eventName) && !subscribedEvents.Contains("*"))
            {
                continue;
            }

            _ = Task.Run(async () =>
            {
                await DeliverWebhookAsync(webhook, payloadJson, cancellationToken);
            }, cancellationToken);
        }
    }

    private async Task DeliverWebhookAsync(
        WebhookSubscription webhook,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, webhook.Url)
            {
                Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
            };

            // Add signature header for verification
            if (!string.IsNullOrEmpty(webhook.Secret))
            {
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                var signaturePayload = $"{timestamp}.{payloadJson}";
                var signature = ComputeHmacSha256(signaturePayload, webhook.Secret);
                request.Headers.Add("X-Webhook-Signature", $"t={timestamp},v1={signature}");
            }

            request.Headers.Add("X-Webhook-Id", webhook.Id.ToString());

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Webhook {Id} delivered successfully to {Url}", webhook.Id, webhook.Url);
                await UpdateWebhookSuccessAsync(webhook.Id, cancellationToken);
            }
            else
            {
                _logger.LogWarning(
                    "Webhook {Id} delivery failed to {Url}: {StatusCode}",
                    webhook.Id, webhook.Url, response.StatusCode);
                await UpdateWebhookFailureAsync(webhook.Id, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error delivering webhook {Id} to {Url}", webhook.Id, webhook.Url);
            await UpdateWebhookFailureAsync(webhook.Id, cancellationToken);
        }
    }

    private async Task UpdateWebhookSuccessAsync(Guid webhookId, CancellationToken cancellationToken)
    {
        try
        {
            var webhook = await _unitOfWork.GetRepository<WebhookSubscription>()
                .GetByIdAsync(webhookId, cancellationToken);

            if (webhook != null)
            {
                webhook.LastSuccessAt = DateTime.UtcNow;
                webhook.FailureCount = 0;
                await _unitOfWork.GetRepository<WebhookSubscription>().UpdateAsync(webhook, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error updating webhook success status for {Id}", webhookId);
        }
    }

    private async Task UpdateWebhookFailureAsync(Guid webhookId, CancellationToken cancellationToken)
    {
        try
        {
            var webhook = await _unitOfWork.GetRepository<WebhookSubscription>()
                .GetByIdAsync(webhookId, cancellationToken);

            if (webhook != null)
            {
                webhook.LastFailureAt = DateTime.UtcNow;
                webhook.FailureCount++;

                // Disable webhook after 10 consecutive failures
                if (webhook.FailureCount >= 10)
                {
                    webhook.IsActive = false;
                    _logger.LogWarning("Webhook {Id} disabled after {Count} consecutive failures", 
                        webhookId, webhook.FailureCount);
                }

                await _unitOfWork.GetRepository<WebhookSubscription>().UpdateAsync(webhook, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error updating webhook failure status for {Id}", webhookId);
        }
    }

    public async Task<WebhookDto> CreateWebhookAsync(
        Guid subscriptionId,
        CreateWebhookRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating webhook {Name} for subscription {SubscriptionId}",
            request.Name, subscriptionId);

        var webhook = new WebhookSubscription
        {
            SubscriptionId = subscriptionId,
            Name = request.Name,
            Url = request.Url,
            Events = request.Events,
            Secret = request.Secret ?? GenerateSecret(),
            IsActive = true
        };

        await _unitOfWork.GetRepository<WebhookSubscription>().AddAsync(webhook, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created webhook {Id} for subscription {SubscriptionId}",
            webhook.Id, subscriptionId);

        return MapToDto(webhook);
    }

    public async Task<List<WebhookDto>> GetWebhooksAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var webhooks = await _unitOfWork.GetRepository<WebhookSubscription>()
            .QueryNoTracking()
            .Where(w => w.SubscriptionId == subscriptionId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(cancellationToken);

        return webhooks.Select(MapToDto).ToList();
    }

    public async Task<WebhookDto?> GetWebhookByIdAsync(
        Guid webhookId,
        CancellationToken cancellationToken = default)
    {
        var webhook = await _unitOfWork.GetRepository<WebhookSubscription>()
            .GetByIdAsync(webhookId, cancellationToken);

        return webhook == null ? null : MapToDto(webhook);
    }

    public async Task<WebhookDto?> UpdateWebhookAsync(
        Guid webhookId,
        UpdateWebhookRequest request,
        CancellationToken cancellationToken = default)
    {
        var webhook = await _unitOfWork.GetRepository<WebhookSubscription>()
            .GetByIdAsync(webhookId, cancellationToken);

        if (webhook == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            webhook.Name = request.Name;
        }

        if (!string.IsNullOrWhiteSpace(request.Url))
        {
            webhook.Url = request.Url;
        }

        if (!string.IsNullOrWhiteSpace(request.Events))
        {
            webhook.Events = request.Events;
        }

        if (request.IsActive.HasValue)
        {
            webhook.IsActive = request.IsActive.Value;
            if (request.IsActive.Value)
            {
                webhook.FailureCount = 0; // Reset failure count when re-enabling
            }
        }

        await _unitOfWork.GetRepository<WebhookSubscription>().UpdateAsync(webhook, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated webhook {Id}", webhookId);

        return MapToDto(webhook);
    }

    public async Task<bool> DeleteWebhookAsync(
        Guid webhookId,
        CancellationToken cancellationToken = default)
    {
        var webhook = await _unitOfWork.GetRepository<WebhookSubscription>()
            .GetByIdAsync(webhookId, cancellationToken);

        if (webhook == null)
        {
            return false;
        }

        await _unitOfWork.GetRepository<WebhookSubscription>().SoftDeleteAsync(webhook, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted webhook {Id}", webhookId);

        return true;
    }

    private static string GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }

    private static string ComputeHmacSha256(string message, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var messageBytes = Encoding.UTF8.GetBytes(message);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(messageBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static WebhookDto MapToDto(WebhookSubscription webhook)
    {
        return new WebhookDto(
            webhook.Id,
            webhook.Name,
            webhook.Url,
            webhook.Events,
            webhook.IsActive,
            webhook.FailureCount,
            webhook.LastSuccessAt,
            webhook.LastFailureAt,
            webhook.CreatedAt
        );
    }
}
