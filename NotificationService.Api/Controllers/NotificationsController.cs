using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NotificationService.Api.Extensions;
using NotificationService.Api.Middleware;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace NotificationService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[RequireSubscriptionKey]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly IValidator<SendNotificationRequest> _validator;
    private readonly IValidator<SendBatchNotificationRequest> _batchValidator;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        INotificationService notificationService,
        IValidator<SendNotificationRequest> validator,
        IValidator<SendBatchNotificationRequest> batchValidator,
        ILogger<NotificationsController> logger)
    {
        _notificationService = notificationService;
        _validator = validator;
        _batchValidator = batchValidator;
        _logger = logger;
    }

    /// <summary>
    /// Send a notification (Email or SMS)
    /// </summary>
    [HttpPost]
    [SwaggerOperation(
        Summary = "Send Notification",
        Description = "Send an email or SMS notification. Requires X-Subscription-Key header.")]
    [SwaggerResponse(201, "Notification created and queued", typeof(SendNotificationResponse))]
    [SwaggerResponse(400, "Validation error")]
    [SwaggerResponse(401, "Invalid or missing subscription key")]
    [SwaggerResponse(429, "Rate limit exceeded")]
    public async Task<ActionResult<SendNotificationResponse>> SendNotification(
        [FromBody] SendNotificationRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var userId = HttpContext.GetUserId();
        var subscriptionId = HttpContext.GetSubscriptionId();

        _logger.LogInformation(
            "Sending {Type} notification to {Recipient} for user {UserId}",
            request.Type, request.Recipient, userId);

        var result = await _notificationService.SendNotificationAsync(
            userId, subscriptionId, request, cancellationToken);

        return CreatedAtAction(
            nameof(GetNotification),
            new { id = result.NotificationId },
            result);
    }

    /// <summary>
    /// Send multiple notifications in a batch
    /// </summary>
    [HttpPost("batch")]
    [SwaggerOperation(
        Summary = "Send Batch Notifications",
        Description = "Send multiple notifications in a single request. Maximum 1000 per batch.")]
    [SwaggerResponse(200, "Batch processed", typeof(SendBatchNotificationResponse))]
    [SwaggerResponse(400, "Validation error")]
    [SwaggerResponse(401, "Invalid or missing subscription key")]
    public async Task<ActionResult<SendBatchNotificationResponse>> SendBatchNotifications(
        [FromBody] SendBatchNotificationRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _batchValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var userId = HttpContext.GetUserId();
        var subscriptionId = HttpContext.GetSubscriptionId();

        _logger.LogInformation(
            "Processing batch of {Count} notifications for user {UserId}",
            request.Notifications.Count, userId);

        var result = await _notificationService.SendBatchNotificationsAsync(
            userId, subscriptionId, request, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Get notification by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [SwaggerOperation(
        Summary = "Get Notification",
        Description = "Get detailed information about a specific notification including logs")]
    [SwaggerResponse(200, "Notification details", typeof(NotificationDetailDto))]
    [SwaggerResponse(404, "Notification not found")]
    public async Task<ActionResult<NotificationDetailDto>> GetNotification(
        Guid id,
        CancellationToken cancellationToken)
    {
        var notification = await _notificationService.GetNotificationByIdAsync(id, cancellationToken);
        if (notification == null)
        {
            return NotFound();
        }

        // Verify ownership
        var userId = HttpContext.GetUserId();
        if (notification.UserId != userId)
        {
            return NotFound();
        }

        return Ok(notification);
    }

    /// <summary>
    /// List notifications with filtering and pagination
    /// </summary>
    [HttpGet]
    [SwaggerOperation(
        Summary = "List Notifications",
        Description = "Get paginated list of notifications with optional filtering")]
    [SwaggerResponse(200, "Notifications list", typeof(PagedResult<NotificationDto>))]
    public async Task<ActionResult<PagedResult<NotificationDto>>> GetNotifications(
        [FromQuery] NotificationQueryRequest query,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetUserId();
        var result = await _notificationService.GetNotificationsAsync(userId, query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Cancel a pending notification
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [SwaggerOperation(
        Summary = "Cancel Notification",
        Description = "Cancel a notification that is still pending. Cannot cancel sent notifications.")]
    [SwaggerResponse(200, "Notification cancelled")]
    [SwaggerResponse(400, "Cannot cancel notification")]
    [SwaggerResponse(404, "Notification not found")]
    public async Task<IActionResult> CancelNotification(Guid id, CancellationToken cancellationToken)
    {
        var result = await _notificationService.CancelNotificationAsync(id, cancellationToken);
        if (!result)
        {
            return BadRequest(new { error = new { code = "CANCEL_FAILED", message = "Cannot cancel this notification" } });
        }

        return Ok(new { message = "Notification cancelled successfully" });
    }

    /// <summary>
    /// Retry a failed notification
    /// </summary>
    [HttpPost("{id:guid}/retry")]
    [SwaggerOperation(
        Summary = "Retry Notification",
        Description = "Retry sending a failed notification")]
    [SwaggerResponse(200, "Notification queued for retry")]
    [SwaggerResponse(400, "Cannot retry notification")]
    [SwaggerResponse(404, "Notification not found")]
    public async Task<IActionResult> RetryNotification(Guid id, CancellationToken cancellationToken)
    {
        var result = await _notificationService.RetryNotificationAsync(id, cancellationToken);
        if (!result)
        {
            return BadRequest(new { error = new { code = "RETRY_FAILED", message = "Cannot retry this notification" } });
        }

        return Ok(new { message = "Notification queued for retry" });
    }

    /// <summary>
    /// Get current subscription quota status
    /// </summary>
    [HttpGet("quota")]
    [SwaggerOperation(
        Summary = "Get Quota Status",
        Description = "Get remaining daily and monthly notification quota for current subscription")]
    [ProducesResponseType(typeof(object), 200)]
    public IActionResult GetQuotaStatus()
    {
        return Ok(new
        {
            remainingDaily = HttpContext.GetRemainingDailyQuota(),
            remainingMonthly = HttpContext.GetRemainingMonthlyQuota(),
            allowSms = HttpContext.CanSendSms(),
            allowEmail = HttpContext.CanSendEmail()
        });
    }
}
