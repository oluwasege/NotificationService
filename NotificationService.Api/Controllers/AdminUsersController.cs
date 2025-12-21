using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace NotificationService.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Produces("application/json")]
[Authorize(Policy = "AdminOnly")]
public class AdminUsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IValidator<CreateUserRequest> _createUserValidator;
    private readonly IValidator<UpdateUserRequest> _updateUserValidator;
    private readonly ILogger<AdminUsersController> _logger;

    public AdminUsersController(
        IUserService userService,
        IValidator<CreateUserRequest> createUserValidator,
        IValidator<UpdateUserRequest> updateUserValidator,
        ILogger<AdminUsersController> logger)
    {
        _userService = userService;
        _createUserValidator = createUserValidator;
        _updateUserValidator = updateUserValidator;
        _logger = logger;
    }

    /// <summary>
    /// Get all users with pagination
    /// </summary>
    [HttpGet]
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
    [HttpGet("{id:guid}")]
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
    [HttpPost]
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
    [HttpPut("{id:guid}")]
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
    [HttpDelete("{id:guid}")]
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
}
