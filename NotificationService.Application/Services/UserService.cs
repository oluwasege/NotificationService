using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Interfaces;

namespace NotificationService.Application.Services;

public class UserService : IUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthService _authService;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IUnitOfWork unitOfWork,
        IAuthService authService,
        ILogger<UserService> logger)
    {
        _unitOfWork = unitOfWork;
        _authService = authService;
        _logger = logger;
    }

    public async Task<PagedResult<UserDto>> GetUsersAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.GetRepository<User>().QueryNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserDto(
                u.Id,
                u.Name,
                u.Email,
                u.Role,
                u.IsActive,
                u.CreatedAt,
                u.LastLoginAt
            ))
            .ToListAsync(cancellationToken);

        return new PagedResult<UserDto>(items, totalCount, page, pageSize, totalPages);
    }

    public async Task<UserDetailDto?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.GetRepository<User>()
            .QueryNoTracking()
            .Include(u => u.Subscriptions)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null) return null;

        return new UserDetailDto(
            user.Id,
            user.Name,
            user.Email,
            user.Role,
            user.IsActive,
            user.CreatedAt,
            user.LastLoginAt,
            user.Subscriptions.Select(s => new SubscriptionDto(
                s.Id,
                s.Name,
                MaskSubscriptionKey(s.SubscriptionKey),
                s.Status,
                s.ExpiresAt,
                s.DailyLimit,
                s.MonthlyLimit,
                s.DailyUsed,
                s.MonthlyUsed,
                s.AllowSms,
                s.AllowEmail,
                s.CreatedAt
            )).ToList()
        );
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating user with email: {Email}", request.Email);

        var existingUser = await _unitOfWork.GetRepository<User>().FirstOrDefaultAsync(
            u => u.Email.ToLower() == request.Email.ToLower(),
            cancellationToken);

        if (existingUser != null)
        {
            throw new InvalidOperationException("A user with this email already exists");
        }

        var user = new User
        {
            Name = request.Name,
            Email = request.Email,
            PasswordHash = _authService.HashPassword(request.Password),
            Role = request.Role,
            IsActive = true
        };

        await _unitOfWork.GetRepository<User>().AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created user {Id} with email: {Email}", user.Id, user.Email);

        return new UserDto(
            user.Id,
            user.Name,
            user.Email,
            user.Role,
            user.IsActive,
            user.CreatedAt,
            user.LastLoginAt
        );
    }

    public async Task<UserDto?> UpdateUserAsync(
        Guid userId,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.GetRepository<User>().GetByIdAsync(userId, cancellationToken);
        if (user == null) return null;

        if (!string.IsNullOrWhiteSpace(request.Name))
            user.Name = request.Name;

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var existingUser = await _unitOfWork.GetRepository<User>().FirstOrDefaultAsync(
                u => u.Email.ToLower() == request.Email.ToLower() && u.Id != userId,
                cancellationToken);

            if (existingUser != null)
                throw new InvalidOperationException("A user with this email already exists");

            user.Email = request.Email;
        }

        if (request.Role.HasValue)
            user.Role = request.Role.Value;

        if (request.IsActive.HasValue)
            user.IsActive = request.IsActive.Value;

        await _unitOfWork.GetRepository<User>().UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated user {Id}", userId);

        return new UserDto(
            user.Id,
            user.Name,
            user.Email,
            user.Role,
            user.IsActive,
            user.CreatedAt,
            user.LastLoginAt
        );
    }

    public async Task<bool> DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.GetRepository<User>().GetByIdAsync(userId, cancellationToken);
        if (user == null) return false;

        await _unitOfWork.GetRepository<User>().SoftDeleteAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Soft deleted user {Id}", userId);
        return true;
    }

    private static string MaskSubscriptionKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length < 10)
            return "***";

        return key[..10] + new string('*', key.Length - 10);
    }
}
