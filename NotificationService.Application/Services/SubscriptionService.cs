using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Interfaces;

namespace NotificationService.Application.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly IRepository<Subscription> _subscriptionRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SubscriptionService> _logger;

    public SubscriptionService(
        IRepository<Subscription> subscriptionRepository,
        IRepository<User> userRepository,
        IUnitOfWork unitOfWork,
        ILogger<SubscriptionService> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PagedResult<SubscriptionDto>> GetSubscriptionsAsync(
        Guid? userId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _subscriptionRepository.QueryNoTracking();

        if (userId.HasValue)
            query = query.Where(s => s.UserId == userId.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SubscriptionDto(
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
            ))
            .ToListAsync(cancellationToken);

        return new PagedResult<SubscriptionDto>(items, totalCount, page, pageSize, totalPages);
    }

    public async Task<SubscriptionDto?> GetSubscriptionByIdAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId, cancellationToken);
        if (subscription == null) return null;

        return new SubscriptionDto(
            subscription.Id,
            subscription.Name,
            MaskSubscriptionKey(subscription.SubscriptionKey),
            subscription.Status,
            subscription.ExpiresAt,
            subscription.DailyLimit,
            subscription.MonthlyLimit,
            subscription.DailyUsed,
            subscription.MonthlyUsed,
            subscription.AllowSms,
            subscription.AllowEmail,
            subscription.CreatedAt
        );
    }

    public async Task<SubscriptionDto> CreateSubscriptionAsync(
        CreateSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating subscription for user {UserId}", request.UserId);

        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
            throw new InvalidOperationException("User not found");

        var subscription = new Subscription
        {
            UserId = request.UserId,
            Name = request.Name,
            SubscriptionKey = GenerateSubscriptionKey(),
            Status = SubscriptionStatus.Active,
            ExpiresAt = DateTime.UtcNow.AddDays(request.ExpiresInDays),
            DailyLimit = request.DailyLimit,
            MonthlyLimit = request.MonthlyLimit,
            AllowSms = request.AllowSms,
            AllowEmail = request.AllowEmail
        };

        await _subscriptionRepository.AddAsync(subscription, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created subscription {Id} for user {UserId}", subscription.Id, request.UserId);

        // Return with visible key for initial creation only
        return new SubscriptionDto(
            subscription.Id,
            subscription.Name,
            subscription.SubscriptionKey,
            subscription.Status,
            subscription.ExpiresAt,
            subscription.DailyLimit,
            subscription.MonthlyLimit,
            subscription.DailyUsed,
            subscription.MonthlyUsed,
            subscription.AllowSms,
            subscription.AllowEmail,
            subscription.CreatedAt
        );
    }

    public async Task<SubscriptionDto?> UpdateSubscriptionAsync(
        Guid subscriptionId,
        UpdateSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId, cancellationToken);
        if (subscription == null) return null;

        if (!string.IsNullOrWhiteSpace(request.Name))
            subscription.Name = request.Name;

        if (request.Status.HasValue)
            subscription.Status = request.Status.Value;

        if (request.DailyLimit.HasValue)
            subscription.DailyLimit = request.DailyLimit.Value;

        if (request.MonthlyLimit.HasValue)
            subscription.MonthlyLimit = request.MonthlyLimit.Value;

        if (request.ExpiresAt.HasValue)
            subscription.ExpiresAt = request.ExpiresAt.Value;

        if (request.AllowSms.HasValue)
            subscription.AllowSms = request.AllowSms.Value;

        if (request.AllowEmail.HasValue)
            subscription.AllowEmail = request.AllowEmail.Value;

        await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated subscription {Id}", subscriptionId);

        return new SubscriptionDto(
            subscription.Id,
            subscription.Name,
            MaskSubscriptionKey(subscription.SubscriptionKey),
            subscription.Status,
            subscription.ExpiresAt,
            subscription.DailyLimit,
            subscription.MonthlyLimit,
            subscription.DailyUsed,
            subscription.MonthlyUsed,
            subscription.AllowSms,
            subscription.AllowEmail,
            subscription.CreatedAt
        );
    }

    public async Task<RegenerateKeyResponse?> RegenerateKeyAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId, cancellationToken);
        if (subscription == null) return null;

        var newKey = GenerateSubscriptionKey();
        subscription.SubscriptionKey = newKey;

        await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Regenerated key for subscription {Id}", subscriptionId);

        return new RegenerateKeyResponse(newKey, DateTime.UtcNow);
    }

    public async Task<bool> DeleteSubscriptionAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId, cancellationToken);
        if (subscription == null) return false;

        await _subscriptionRepository.SoftDeleteAsync(subscription, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Soft deleted subscription {Id}", subscriptionId);
        return true;
    }

    private static string GenerateSubscriptionKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        var key = Convert.ToBase64String(bytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "");
        return $"sk_live_{key}";
    }

    private static string MaskSubscriptionKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length < 10)
            return "***";

        return key[..10] + new string('*', key.Length - 10);
    }
}
