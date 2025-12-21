using NotificationService.Application.DTOs;

namespace NotificationService.Application.Interfaces;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<UserDto?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
}
