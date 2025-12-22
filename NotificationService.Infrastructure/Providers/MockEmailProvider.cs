using Microsoft.Extensions.Logging;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Interfaces;
using Polly;
using Polly.CircuitBreaker;

namespace NotificationService.Infrastructure.Providers;

public class MockEmailProvider : INotificationProvider
{
    private readonly ILogger<MockEmailProvider> _logger;
    private static readonly Random _random = new();
    private readonly bool _isAvailable = true;
    private readonly ResiliencePipeline _resiliencePipeline;

    public string ProviderName => "MockEmailProvider";
    public bool IsAvailable => _isAvailable && !_circuitBroken;
    private bool _circuitBroken = false;

    public MockEmailProvider(ILogger<MockEmailProvider> logger)
    {
        _logger = logger;

        // Configure resilience pipeline with circuit breaker
        _resiliencePipeline = new ResiliencePipelineBuilder()
            .AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromMilliseconds(200),
                BackoffType = DelayBackoffType.Exponential,
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "Retry {AttemptNumber} for email provider after {Delay}ms",
                        args.AttemptNumber,
                        args.RetryDelay.TotalMilliseconds);
                    return ValueTask.CompletedTask;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(30),
                OnOpened = args =>
                {
                    _circuitBroken = true;
                    _logger.LogWarning("Circuit breaker OPENED for email provider");
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    _circuitBroken = false;
                    _logger.LogInformation("Circuit breaker CLOSED for email provider");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    _logger.LogInformation("Circuit breaker HALF-OPEN for email provider");
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(TimeSpan.FromSeconds(10))
            .Build();
    }

    public async Task<NotificationResult> SendAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _resiliencePipeline.ExecuteAsync(async token =>
            {
                return await SendInternalAsync(notification, token);
            }, cancellationToken);
        }
        catch (BrokenCircuitException)
        {
            _logger.LogError("Circuit breaker is open - email provider temporarily unavailable");
            return new NotificationResult(
                Success: false,
                Message: "Email provider temporarily unavailable (circuit breaker open)",
                ProviderResponse: "{\"status\":\"circuit_open\",\"error\":\"Provider temporarily unavailable\"}"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email to {Recipient}", notification.Recipient);
            return new NotificationResult(
                Success: false,
                Message: ex.Message,
                ProviderResponse: $"{{\"status\":\"error\",\"error\":\"{ex.Message}\"}}"
            );
        }
    }

    private async Task<NotificationResult> SendInternalAsync(Notification notification, CancellationToken cancellationToken)
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

            throw new InvalidOperationException("Simulated email delivery failure");
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

    public Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("MockEmail: Health check - Available: {IsAvailable}", IsAvailable);
        return Task.FromResult(IsAvailable);
    }
}
