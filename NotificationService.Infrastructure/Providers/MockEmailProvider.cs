using Microsoft.Extensions.Logging;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Interfaces;

namespace NotificationService.Infrastructure.Providers;

public class MockEmailProvider : INotificationProvider
{
    private readonly ILogger<MockEmailProvider> _logger;
    private static readonly Random _random = new();

    public string ProviderName => "MockEmailProvider";

    public MockEmailProvider(ILogger<MockEmailProvider> logger)
    {
        _logger = logger;
    }

    public async Task<NotificationResult> SendAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[MockEmailProvider] Sending email to {Recipient} with subject: {Subject}",
            notification.Recipient,
            notification.Subject);

        // Simulate network latency
        await Task.Delay(_random.Next(50, 200), cancellationToken);

        // Simulate 95% success rate
        var success = _random.Next(100) < 95;
        var externalId = Guid.NewGuid().ToString("N");

        if (success)
        {
            _logger.LogInformation(
                "[MockEmailProvider] Email sent successfully to {Recipient}. ExternalId: {ExternalId}",
                notification.Recipient,
                externalId);

            return new NotificationResult(
                Success: true,
                ExternalId: externalId,
                Message: "Email sent successfully via MockEmailProvider",
                ProviderResponse: $"{{\"status\":\"sent\",\"messageId\":\"{externalId}\",\"timestamp\":\"{DateTime.UtcNow:O}\"}}"
            );
        }
        else
        {
            _logger.LogWarning(
                "[MockEmailProvider] Failed to send email to {Recipient}. Simulated failure.",
                notification.Recipient);

            return new NotificationResult(
                Success: false,
                Message: "Simulated email delivery failure",
                ProviderResponse: "{\"status\":\"failed\",\"error\":\"SMTP_TIMEOUT\",\"code\":\"E_TIMEOUT\"}"
            );
        }
    }

    public async Task<NotificationResult> GetStatusAsync(string externalId, CancellationToken cancellationToken = default)
    {
        await Task.Delay(50, cancellationToken);

        _logger.LogInformation("[MockEmailProvider] Checking status for {ExternalId}", externalId);

        return new NotificationResult(
            Success: true,
            ExternalId: externalId,
            Message: "Email delivered",
            ProviderResponse: $"{{\"status\":\"delivered\",\"deliveredAt\":\"{DateTime.UtcNow:O}\"}}"
        );
    }
}
