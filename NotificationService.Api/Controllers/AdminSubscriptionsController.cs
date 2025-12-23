using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace NotificationService.Api.Controllers;

[ApiController]
[Route("api/admin/subscriptions")]
[Produces("application/json")]
[Authorize(Policy = "AdminOnly")]
public class AdminSubscriptionsController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly IValidator<CreateSubscriptionRequest> _createSubscriptionValidator;
    private readonly IValidator<UpdateSubscriptionRequest> _updateSubscriptionValidator;
    private readonly ILogger<AdminSubscriptionsController> _logger;

    public AdminSubscriptionsController(
        ISubscriptionService subscriptionService,
        IValidator<CreateSubscriptionRequest> createSubscriptionValidator,
        IValidator<UpdateSubscriptionRequest> updateSubscriptionValidator,
        ILogger<AdminSubscriptionsController> logger)
    {
        _subscriptionService = subscriptionService;
        _createSubscriptionValidator = createSubscriptionValidator;
        _updateSubscriptionValidator = updateSubscriptionValidator;
        _logger = logger;
    }

    /// <summary>
    /// Get all subscriptions with pagination
    /// </summary>
    [HttpGet]
    [SwaggerOperation(Summary = "List Subscriptions", Description = "Get paginated list of all subscriptions")]
    [ProducesResponseType(typeof(PagedResult<SubscriptionDto>), StatusCodes.Status200OK)]
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
    [HttpGet("{id:guid}")]
    [SwaggerOperation(Summary = "Get Subscription", Description = "Get subscription details")]
    [ProducesResponseType(typeof(SubscriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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
    [HttpPost]
    [SwaggerOperation(
        Summary = "Create Subscription",
        Description = "Create a new subscription. Returns the full subscription key (only visible once)")]
    [ProducesResponseType(typeof(SubscriptionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
    [HttpPut("{id:guid}")]
    [SwaggerOperation(Summary = "Update Subscription", Description = "Update subscription settings")]
    [ProducesResponseType(typeof(SubscriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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
    [HttpPost("{id:guid}/regenerate-key")]
    [SwaggerOperation(
        Summary = "Regenerate Subscription Key",
        Description = "Generate a new subscription key. Old key will be invalidated immediately.")]
    [ProducesResponseType(typeof(RegenerateKeyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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
    [HttpDelete("{id:guid}")]
    [SwaggerOperation(Summary = "Delete Subscription", Description = "Soft delete a subscription")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSubscription(Guid id, CancellationToken cancellationToken)
    {
        var result = await _subscriptionService.DeleteSubscriptionAsync(id, cancellationToken);
        if (!result)
        {
            return NotFound();
        }
        return NoContent();
    }
}
