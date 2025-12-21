using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace NotificationService.Api.Controllers;

[ApiController]
[Route("api/admin/dashboard")]
[Produces("application/json")]
[Authorize(Policy = "AdminOnly")]
public class AdminDashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;
    private readonly ILogger<AdminDashboardController> _logger;

    public AdminDashboardController(
        IDashboardService dashboardService,
        ILogger<AdminDashboardController> logger)
    {
        _dashboardService = dashboardService;
        _logger = logger;
    }

    /// <summary>
    /// Get dashboard summary with system metrics
    /// </summary>
    [HttpGet]
    [SwaggerOperation(
        Summary = "Get Dashboard Summary",
        Description = "Get comprehensive dashboard with user, subscription, and notification statistics")]
    [SwaggerResponse(200, "Dashboard data", typeof(DashboardSummaryDto))]
    [SwaggerResponse(401, "Unauthorized")]
    [SwaggerResponse(403, "Forbidden - Admin access required")]
    public async Task<ActionResult<DashboardSummaryDto>> GetDashboard(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin {User} accessing dashboard", User.Identity?.Name);
        var summary = await _dashboardService.GetDashboardSummaryAsync(cancellationToken);
        return Ok(summary);
    }

    /// <summary>
    /// Get top users by notification volume
    /// </summary>
    [HttpGet("top-users")]
    [SwaggerOperation(Summary = "Get Top Users", Description = "Get top users by notification count")]
    [SwaggerResponse(200, "Top users list", typeof(List<UserStatsDto>))]
    public async Task<ActionResult<List<UserStatsDto>>> GetTopUsers(
        [FromQuery] int count = 10,
        CancellationToken cancellationToken = default)
    {
        var users = await _dashboardService.GetTopUsersAsync(count, cancellationToken);
        return Ok(users);
    }

    /// <summary>
    /// Get notification statistics for a date range
    /// </summary>
    [HttpGet("stats")]
    [SwaggerOperation(Summary = "Get Notification Stats", Description = "Get daily notification statistics")]
    [SwaggerResponse(200, "Statistics", typeof(List<DailyNotificationStatsDto>))]
    public async Task<ActionResult<List<DailyNotificationStatsDto>>> GetNotificationStats(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken = default)
    {
        var from = fromDate ?? DateTime.UtcNow.AddDays(-30);
        var to = toDate ?? DateTime.UtcNow;
        var stats = await _dashboardService.GetNotificationStatsAsync(from, to, cancellationToken);
        return Ok(stats);
    }
}
