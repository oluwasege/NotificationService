using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace NotificationService.Api.Controllers;

[ApiController]
[Route("api/admin/notifications")]
[Produces("application/json")]
[Authorize(Policy = "AdminOnly")]
public class AdminNotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<AdminNotificationsController> _logger;

    public AdminNotificationsController(
        INotificationService notificationService,
        ILogger<AdminNotificationsController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Get all notifications (admin view)
    /// </summary>
    [HttpGet]
    [SwaggerOperation(Summary = "List All Notifications", Description = "Get all notifications across all users")]
    [ProducesResponseType(typeof(PagedResult<NotificationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<NotificationDto>>> GetAllNotifications(
        [FromQuery] NotificationQueryRequest query,
        CancellationToken cancellationToken)
    {
        var result = await _notificationService.GetNotificationsAsync(null, query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get notification details (admin view)
    /// </summary>
    [HttpGet("{id:guid}")]
    [SwaggerOperation(Summary = "Get Notification Details", Description = "Get notification details with logs")]
    [ProducesResponseType(typeof(NotificationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NotificationDetailDto>> GetNotificationDetails(
        Guid id,
        CancellationToken cancellationToken)
    {
        var notification = await _notificationService.GetNotificationByIdAsync(id, cancellationToken);
        if (notification == null)
        {
            return NotFound();
        }
        return Ok(notification);
    }
}
