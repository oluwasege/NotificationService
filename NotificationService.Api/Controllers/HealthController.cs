using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.DTOs;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Interfaces;
using NotificationService.Infrastructure.Data;
using NotificationService.Infrastructure.Providers;
using Swashbuckle.AspNetCore.Annotations;

namespace NotificationService.Api.Controllers;

/// <summary>
/// Health check and diagnostics endpoints.
/// Note: Basic health checks are also available at /health, /health/live, /health/ready via ASP.NET Health Checks framework.
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
    /// Gets detailed system health status including providers.
    /// </summary>
    /// <remarks>
    /// For basic health checks, use:
    /// - GET /health - Full health check with all registered checks
    /// - GET /health/live - Liveness probe (always returns healthy)
    /// - GET /health/ready - Readiness probe (checks database)
    /// </remarks>
    [HttpGet("detailed")]
    [SwaggerOperation(
        Summary = "Get Detailed Health Status",
        Description = "Gets comprehensive health status including database, providers, and queue metrics")]
    [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetDetailedHealth(CancellationToken cancellationToken)
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
                ? await _unitOfWork.GetRepository<OutboxMessage>().CountAsync(x => x.ProcessedAt == null, cancellationToken)
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
            Timestamp = DateTime.UtcNow,
            Status = isHealthy ? "Healthy" : "Unhealthy",
            Database = databaseHealth,
            Providers = providerHealths,
            Queue = queueHealth
        };

        return isHealthy ? Ok(response) : StatusCode(503, response);
    }

    /// <summary>
    /// Gets system statistics.
    /// </summary>
    [HttpGet("stats")]
    [SwaggerOperation(
        Summary = "Get System Statistics",
        Description = "Gets notification statistics by status and success rates")]
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
