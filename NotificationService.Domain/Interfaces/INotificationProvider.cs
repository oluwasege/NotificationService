using NotificationService.Domain.Entities;

namespace NotificationService.Domain.Interfaces;

public interface INotificationProvider
{
    string ProviderName { get; }
    Task<NotificationResult> SendAsync(Notification notification, CancellationToken cancellationToken = default);
    Task<NotificationResult> GetStatusAsync(string externalId, CancellationToken cancellationToken = default);
}

public record NotificationResult(
    bool Success,
    string? ExternalId = null,
    string? Message = null,
    string? ProviderResponse = null
);
