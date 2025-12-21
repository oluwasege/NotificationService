using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.DTOs;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Interfaces;
using NotificationService.Infrastructure.Data;
using NotificationService.Infrastructure.Providers;
using NotificationService.Infrastructure.Queue;

namespace NotificationService.Api.Controllers;

/// <summary>
/// Health check and diagnostics endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class HealthController : ControllerBase
{
    private readonly NotificationDbContext _dbContext;
    private readonly NotificationProviderFactory _providerFactory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationQueue _messageQueue;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        NotificationDbContext dbContext,
        NotificationProviderFactory providerFactory,
        IUnitOfWork unitOfWork,
        INotificationQueue messageQueue,
        ILogger<HealthController> logger)
    {
        _dbContext = dbContext;
        _providerFactory = providerFactory;
        _unitOfWork = unitOfWork;
        _messageQueue = messageQueue;
        _logger = logger;
    }

    /// <summary>
    /// Gets system health status.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetHealth(CancellationToken cancellationToken)
    {
        // Check database
        DatabaseHealthResponse databaseHealth;
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            var pendingCount = canConnect
                ? await _unitOfWork.GetRepository<Notification>().CountAsync(x => x.Status == NotificationStatus.Pending, cancellationToken)
                : 0;
            var outboxCount = canConnect
                ? await _unitOfWork.GetRepository<Notification>().CountAsync(x => x.Status == NotificationStatus.Sent, cancellationToken)
                : 0;

            databaseHealth = new DatabaseHealthResponse
            {
                IsConnected = canConnect,
                PendingNotifications = pendingCount,
                UnprocessedOutbox = outboxCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            databaseHealth = new DatabaseHealthResponse { IsConnected = false };
        }

        // Check providers
        var providerHealths = new Dictionary<string, ProviderHealthResponse>();
        var providerTypes = Enum.GetValues<NotificationType>();
        foreach (var type in providerTypes)
        {
            var provider = _providerFactory.GetProvider(type);
            if (provider != null)
            {
                try
                {
                    var providerHealthy = await provider.HealthCheckAsync(cancellationToken);
                    providerHealths[provider.ProviderName] = new ProviderHealthResponse
                    {
                        Name = provider.ProviderName,
                        Type = type.ToString(),
                        IsAvailable = providerHealthy
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Provider {Provider} health check failed", provider.ProviderName);
                    providerHealths[provider.ProviderName] = new ProviderHealthResponse
                    {
                        Name = provider.ProviderName,
                        Type = type.ToString(),
                        IsAvailable = false
                    };
                }
            }
        }

        // Check queue
        var queueHealth = new QueueHealthResponse
        {
            TotalDepth = _messageQueue.GetQueueCount(),
            PendingCount = _messageQueue.GetQueueDepth(NotificationPriority.Normal)
        };

        // Determine overall status
        var isHealthy = databaseHealth.IsConnected &&
                        providerHealths.Values.Any(p => p.IsAvailable);

        var response = new HealthCheckResponse
        {
            Timestamp = DateTime.Now,
            Status = isHealthy ? "Healthy" : "Unhealthy",
            Database = databaseHealth,
            Providers = providerHealths,
            Queue = queueHealth
        };

        return isHealthy ? Ok(response) : StatusCode(503, response);
    }

    /// <summary>
    /// Simple liveness probe.
    /// </summary>
    [HttpGet("live")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult Live() => Ok(new { status = "alive", timestamp = DateTime.UtcNow });

    /// <summary>
    /// Readiness probe - checks if service can handle requests.
    /// </summary>
    [HttpGet("ready")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Ready(CancellationToken cancellationToken)
    {
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
                return StatusCode(503, new { status = "not ready", reason = "database unavailable" });

            return Ok(new { status = "ready", timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Readiness check failed");
            return StatusCode(503, new { status = "not ready", reason = ex.Message });
        }
    }

    /// <summary>
    /// Gets system statistics.
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApiResponse<StatisticsResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatistics(CancellationToken cancellationToken)
    {
        // Get counts by status
        var notificationsByStatus = new Dictionary<string, int>();
        foreach (var status in Enum.GetValues<NotificationStatus>())
        {
            var count = await _unitOfWork.GetRepository<Notification>().CountAsync(x => x.Status == status, cancellationToken);
            notificationsByStatus[status.ToString()] = count;
        }

        // Calculate today's stats
        var totalToday = notificationsByStatus.Values.Sum();
        var deliveredToday = notificationsByStatus.GetValueOrDefault("Delivered", 0);
        var failedToday = notificationsByStatus.GetValueOrDefault("Failed", 0);

        var total = deliveredToday + failedToday;
        var successRate = total > 0 ? Math.Round((double)deliveredToday / total * 100, 2) : 100;

        var stats = new StatisticsResponse
        {
            GeneratedAt = DateTime.UtcNow,
            NotificationsByStatus = notificationsByStatus,
            TotalToday = totalToday,
            DeliveredToday = deliveredToday,
            FailedToday = failedToday,
            SuccessRate = successRate
        };

        return Ok(ApiResponse<StatisticsResponse>.Ok(stats));
    }
}
