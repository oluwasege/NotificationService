using NotificationService.Domain.Enums;

namespace NotificationService.Application.DTOs;

public record UserDto(
    Guid Id,
    string Name,
    string Email,
    UserRole Role,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt
);

public record UserDetailDto(
    Guid Id,
    string Name,
    string Email,
    UserRole Role,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    List<SubscriptionDto> Subscriptions
);

public record CreateUserRequest(
    string Name,
    string Email,
    string Password,
    UserRole Role = UserRole.User
);

public record UpdateUserRequest(
    string? Name = null,
    string? Email = null,
    UserRole? Role = null,
    bool? IsActive = null
);
