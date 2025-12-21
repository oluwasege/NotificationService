using NotificationService.Application.DTOs;

namespace NotificationService.Application.Interfaces;

public interface IUserService
{
    Task<PagedResult<UserDto>> GetUsersAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<UserDetailDto?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task<UserDto?> UpdateUserAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
