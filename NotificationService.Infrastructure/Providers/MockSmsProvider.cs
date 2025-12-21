using Microsoft.Extensions.Logging;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Interfaces;

namespace NotificationService.Infrastructure.Providers;

public class MockSmsProvider : INotificationProvider
{
    private readonly ILogger<MockSmsProvider> _logger;
    private static readonly Random _random = new();

    public string ProviderName => "MockSmsProvider";

    public MockSmsProvider(ILogger<MockSmsProvider> logger)
    {
        _logger = logger;
    }

    public async Task<NotificationResult> SendAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[MockSmsProvider] Sending SMS to {Recipient}",
            notification.Recipient);

        // Simulate network latency
        await Task.Delay(_random.Next(100, 300), cancellationToken);

        // Simulate 93% success rate for SMS
        var success = _random.Next(100) < 93;
        var externalId = $"SMS_{Guid.NewGuid():N}"[..24];

        if (success)
        {
            _logger.LogInformation(
                "[MockSmsProvider] SMS sent successfully to {Recipient}. ExternalId: {ExternalId}",
                notification.Recipient,
                externalId);

            return new NotificationResult(
                Success: true,
                ExternalId: externalId,
                Message: "SMS sent successfully via MockSmsProvider",
                ProviderResponse: $"{{\"status\":\"queued\",\"sid\":\"{externalId}\",\"from\":\"+1234567890\",\"to\":\"{notification.Recipient}\"}}"
            );
        }
        else
        {
            _logger.LogWarning(
                "[MockSmsProvider] Failed to send SMS to {Recipient}. Simulated failure.",
                notification.Recipient);

            return new NotificationResult(
                Success: false,
                Message: "Simulated SMS delivery failure",
                ProviderResponse: "{\"status\":\"failed\",\"error\":\"CARRIER_REJECTED\",\"code\":\"21610\"}"
            );
        }
    }

    public async Task<NotificationResult> GetStatusAsync(string externalId, CancellationToken cancellationToken = default)
    {
        await Task.Delay(50, cancellationToken);

        _logger.LogInformation("[MockSmsProvider] Checking status for {ExternalId}", externalId);

        return new NotificationResult(
            Success: true,
            ExternalId: externalId,
            Message: "SMS delivered",
            ProviderResponse: $"{{\"status\":\"delivered\",\"deliveredAt\":\"{DateTime.UtcNow:O}\"}}"
        );
    }
}
