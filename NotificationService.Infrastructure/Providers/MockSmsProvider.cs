using Microsoft.Extensions.Logging;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Interfaces;
using Polly;
using Polly.CircuitBreaker;

namespace NotificationService.Infrastructure.Providers;

public class MockSmsProvider : INotificationProvider
{
    private readonly ILogger<MockSmsProvider> _logger;
    private static readonly Random _random = new();
    private readonly bool _isAvailable = true;
    private readonly ResiliencePipeline _resiliencePipeline;

    public string ProviderName => "MockSmsProvider";
    public bool IsAvailable => _isAvailable && !_circuitBroken;
    private bool _circuitBroken = false;

    public MockSmsProvider(ILogger<MockSmsProvider> logger)
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
                        "Retry {AttemptNumber} for SMS provider after {Delay}ms",
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
                    _logger.LogWarning("Circuit breaker OPENED for SMS provider");
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    _circuitBroken = false;
                    _logger.LogInformation("Circuit breaker CLOSED for SMS provider");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    _logger.LogInformation("Circuit breaker HALF-OPEN for SMS provider");
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
            _logger.LogError("Circuit breaker is open - SMS provider temporarily unavailable");
            return new NotificationResult(
                Success: false,
                Message: "SMS provider temporarily unavailable (circuit breaker open)",
                ProviderResponse: "{\"status\":\"circuit_open\",\"error\":\"Provider temporarily unavailable\"}"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending SMS to {Recipient}", notification.Recipient);
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

            throw new InvalidOperationException("Simulated SMS delivery failure");
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

    public Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("MockSms: Health check - Available: {IsAvailable}", IsAvailable);
        return Task.FromResult(IsAvailable);
    }
}
