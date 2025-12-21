using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Enums;
using Swashbuckle.AspNetCore.Annotations;

namespace NotificationService.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Produces("application/json")]
[Authorize(Policy = "AdminOnly")]
public class AdminController : ControllerBase
{
    private readonly IDashboardService _dashboardService;
    private readonly IUserService _userService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly INotificationService _notificationService;
    private readonly IValidator<CreateUserRequest> _createUserValidator;
    private readonly IValidator<UpdateUserRequest> _updateUserValidator;
    private readonly IValidator<CreateSubscriptionRequest> _createSubscriptionValidator;
    private readonly IValidator<UpdateSubscriptionRequest> _updateSubscriptionValidator;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IDashboardService dashboardService,
        IUserService userService,
        ISubscriptionService subscriptionService,
        INotificationService notificationService,
        IValidator<CreateUserRequest> createUserValidator,
        IValidator<UpdateUserRequest> updateUserValidator,
        IValidator<CreateSubscriptionRequest> createSubscriptionValidator,
        IValidator<UpdateSubscriptionRequest> updateSubscriptionValidator,
        ILogger<AdminController> logger)
    {
        _dashboardService = dashboardService;
        _userService = userService;
        _subscriptionService = subscriptionService;
        _notificationService = notificationService;
        _createUserValidator = createUserValidator;
        _updateUserValidator = updateUserValidator;
        _createSubscriptionValidator = createSubscriptionValidator;
        _updateSubscriptionValidator = updateSubscriptionValidator;
        _logger = logger;
    }

    #region Dashboard

    /// <summary>
    /// Get dashboard summary with system metrics
    /// </summary>
    [HttpGet("dashboard")]
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
    [HttpGet("dashboard/top-users")]
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
    [HttpGet("dashboard/stats")]
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

    #endregion

    #region Users

    /// <summary>
    /// Get all users with pagination
    /// </summary>
    [HttpGet("users")]
    [SwaggerOperation(Summary = "List Users", Description = "Get paginated list of all users")]
    [SwaggerResponse(200, "Users list", typeof(PagedResult<UserDto>))]
    public async Task<ActionResult<PagedResult<UserDto>>> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var users = await _userService.GetUsersAsync(page, pageSize, cancellationToken);
        return Ok(users);
    }

    /// <summary>
    /// Get user by ID with subscriptions
    /// </summary>
    [HttpGet("users/{id:guid}")]
    [SwaggerOperation(Summary = "Get User", Description = "Get user details including subscriptions")]
    [SwaggerResponse(200, "User details", typeof(UserDetailDto))]
    [SwaggerResponse(404, "User not found")]
    public async Task<ActionResult<UserDetailDto>> GetUser(Guid id, CancellationToken cancellationToken)
    {
        var user = await _userService.GetUserByIdAsync(id, cancellationToken);
        if (user == null)
        {
            return NotFound();
        }
        return Ok(user);
    }

    /// <summary>
    /// Create a new user
    /// </summary>
    [HttpPost("users")]
    [Authorize(Policy = "SuperAdminOnly")]
    [SwaggerOperation(Summary = "Create User", Description = "Create a new user (SuperAdmin only)")]
    [SwaggerResponse(201, "User created", typeof(UserDto))]
    [SwaggerResponse(400, "Validation error")]
    public async Task<ActionResult<UserDto>> CreateUser(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _createUserValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var user = await _userService.CreateUserAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
    }

    /// <summary>
    /// Update user
    /// </summary>
    [HttpPut("users/{id:guid}")]
    [SwaggerOperation(Summary = "Update User", Description = "Update user details")]
    [SwaggerResponse(200, "User updated", typeof(UserDto))]
    [SwaggerResponse(404, "User not found")]
    public async Task<ActionResult<UserDto>> UpdateUser(
        Guid id,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _updateUserValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var user = await _userService.UpdateUserAsync(id, request, cancellationToken);
        if (user == null)
        {
            return NotFound();
        }
        return Ok(user);
    }

    /// <summary>
    /// Delete user (soft delete)
    /// </summary>
    [HttpDelete("users/{id:guid}")]
    [Authorize(Policy = "SuperAdminOnly")]
    [SwaggerOperation(Summary = "Delete User", Description = "Soft delete a user (SuperAdmin only)")]
    [SwaggerResponse(204, "User deleted")]
    [SwaggerResponse(404, "User not found")]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken cancellationToken)
    {
        var result = await _userService.DeleteUserAsync(id, cancellationToken);
        if (!result)
        {
            return NotFound();
        }
        return NoContent();
    }

    #endregion

    #region Subscriptions

    /// <summary>
    /// Get all subscriptions with pagination
    /// </summary>
    [HttpGet("subscriptions")]
    [SwaggerOperation(Summary = "List Subscriptions", Description = "Get paginated list of all subscriptions")]
    [SwaggerResponse(200, "Subscriptions list", typeof(PagedResult<SubscriptionDto>))]
    public async Task<ActionResult<PagedResult<SubscriptionDto>>> GetSubscriptions(
        [FromQuery] Guid? userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var subscriptions = await _subscriptionService.GetSubscriptionsAsync(userId, page, pageSize, cancellationToken);
        return Ok(subscriptions);
    }

    /// <summary>
    /// Get subscription by ID
    /// </summary>
    [HttpGet("subscriptions/{id:guid}")]
    [SwaggerOperation(Summary = "Get Subscription", Description = "Get subscription details")]
    [SwaggerResponse(200, "Subscription details", typeof(SubscriptionDto))]
    [SwaggerResponse(404, "Subscription not found")]
    public async Task<ActionResult<SubscriptionDto>> GetSubscription(Guid id, CancellationToken cancellationToken)
    {
        var subscription = await _subscriptionService.GetSubscriptionByIdAsync(id, cancellationToken);
        if (subscription == null)
        {
            return NotFound();
        }
        return Ok(subscription);
    }

    /// <summary>
    /// Create a new subscription for a user
    /// </summary>
    [HttpPost("subscriptions")]
    [SwaggerOperation(
        Summary = "Create Subscription",
        Description = "Create a new subscription. Returns the full subscription key (only visible once)")]
    [SwaggerResponse(201, "Subscription created with key", typeof(SubscriptionDto))]
    [SwaggerResponse(400, "Validation error")]
    public async Task<ActionResult<SubscriptionDto>> CreateSubscription(
        [FromBody] CreateSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _createSubscriptionValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        _logger.LogInformation("Creating subscription for user {UserId} by admin {Admin}",
            request.UserId, User.Identity?.Name);

        var subscription = await _subscriptionService.CreateSubscriptionAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetSubscription), new { id = subscription.Id }, subscription);
    }

    /// <summary>
    /// Update subscription
    /// </summary>
    [HttpPut("subscriptions/{id:guid}")]
    [SwaggerOperation(Summary = "Update Subscription", Description = "Update subscription settings")]
    [SwaggerResponse(200, "Subscription updated", typeof(SubscriptionDto))]
    [SwaggerResponse(404, "Subscription not found")]
    public async Task<ActionResult<SubscriptionDto>> UpdateSubscription(
        Guid id,
        [FromBody] UpdateSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _updateSubscriptionValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var subscription = await _subscriptionService.UpdateSubscriptionAsync(id, request, cancellationToken);
        if (subscription == null)
        {
            return NotFound();
        }
        return Ok(subscription);
    }

    /// <summary>
    /// Regenerate subscription key
    /// </summary>
    [HttpPost("subscriptions/{id:guid}/regenerate-key")]
    [SwaggerOperation(
        Summary = "Regenerate Subscription Key",
        Description = "Generate a new subscription key. Old key will be invalidated immediately.")]
    [SwaggerResponse(200, "New key generated", typeof(RegenerateKeyResponse))]
    [SwaggerResponse(404, "Subscription not found")]
    public async Task<ActionResult<RegenerateKeyResponse>> RegenerateSubscriptionKey(
        Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning("Regenerating subscription key for {SubscriptionId} by admin {Admin}",
            id, User.Identity?.Name);

        var result = await _subscriptionService.RegenerateKeyAsync(id, cancellationToken);
        if (result == null)
        {
            return NotFound();
        }
        return Ok(result);
    }

    /// <summary>
    /// Delete subscription (soft delete)
    /// </summary>
    [HttpDelete("subscriptions/{id:guid}")]
    [SwaggerOperation(Summary = "Delete Subscription", Description = "Soft delete a subscription")]
    [SwaggerResponse(204, "Subscription deleted")]
    [SwaggerResponse(404, "Subscription not found")]
    public async Task<IActionResult> DeleteSubscription(Guid id, CancellationToken cancellationToken)
    {
        var result = await _subscriptionService.DeleteSubscriptionAsync(id, cancellationToken);
        if (!result)
        {
            return NotFound();
        }
        return NoContent();
    }

    #endregion

    #region Notifications (Admin View)

    /// <summary>
    /// Get all notifications (admin view)
    /// </summary>
    [HttpGet("notifications")]
    [SwaggerOperation(Summary = "List All Notifications", Description = "Get all notifications across all users")]
    [SwaggerResponse(200, "Notifications list", typeof(PagedResult<NotificationDto>))]
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
    [HttpGet("notifications/{id:guid}")]
    [SwaggerOperation(Summary = "Get Notification Details", Description = "Get notification details with logs")]
    [SwaggerResponse(200, "Notification details", typeof(NotificationDetailDto))]
    [SwaggerResponse(404, "Notification not found")]
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

    #endregion
}
