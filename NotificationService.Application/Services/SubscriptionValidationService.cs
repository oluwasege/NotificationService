using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Exceptions;
using NotificationService.Domain.Interfaces;

namespace NotificationService.Application.Services;

public class SubscriptionValidationService : ISubscriptionValidationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SubscriptionValidationService> _logger;
    private const int MaxConcurrencyRetries = 3;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public SubscriptionValidationService(
        IUnitOfWork unitOfWork,
        IMemoryCache cache,
        ILogger<SubscriptionValidationService> logger)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _logger = logger;
    }

    public async Task<SubscriptionKeyValidationResult> ValidateSubscriptionKeyAsync(
        string subscriptionKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subscriptionKey))
        {
            _logger.LogWarning("Empty subscription key provided");
            return new SubscriptionKeyValidationResult(false, null, null, "Subscription key is required", null, null, null, null);
        }

        // Try cache first
        var cacheKey = $"subscription:{subscriptionKey}";
        if (_cache.TryGetValue<SubscriptionCacheEntry>(cacheKey, out var cached) && cached != null)
        {
            _logger.LogDebug("Subscription {Id} found in cache", cached.SubscriptionId);
            
            // Still need to validate dynamic fields (quota, expiry)
            return await ValidateCachedSubscriptionAsync(cached, cancellationToken);
        }

        var subscription = await _unitOfWork.GetRepository<Subscription>()      
            .QueryNoTracking()
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.SubscriptionKey == subscriptionKey, cancellationToken);

        if (subscription == null)
        {
            _logger.LogWarning("Invalid subscription key: {Key}", subscriptionKey[..Math.Min(10, subscriptionKey.Length)] + "***");
            return new SubscriptionKeyValidationResult(false, null, null, "Invalid subscription key", null, null, null, null);
        }

        // Cache the subscription data
        var cacheEntry = new SubscriptionCacheEntry(
            subscription.Id,
            subscription.UserId,
            subscription.Status,
            subscription.ExpiresAt,
            subscription.AllowSms,
            subscription.AllowEmail,
            subscription.User.IsActive
        );
        _cache.Set(cacheKey, cacheEntry, CacheDuration);

        return await ValidateSubscriptionAsync(subscription, cancellationToken);
    }

    private async Task<SubscriptionKeyValidationResult> ValidateCachedSubscriptionAsync(
        SubscriptionCacheEntry cached,
        CancellationToken cancellationToken)
    {
        if (cached.Status != SubscriptionStatus.Active)
        {
            return new SubscriptionKeyValidationResult(false, null, null, $"Subscription is {cached.Status}", null, null, null, null);
        }

        if (cached.ExpiresAt < DateTime.UtcNow)
        {
            return new SubscriptionKeyValidationResult(false, null, null, "Subscription has expired", null, null, null, null);
        }

        if (!cached.UserIsActive)
        {
            return new SubscriptionKeyValidationResult(false, null, null, "User account is inactive", null, null, null, null);
        }

        // Get fresh quota data
        var subscription = await _unitOfWork.GetRepository<Subscription>()
            .QueryNoTracking()
            .FirstOrDefaultAsync(s => s.Id == cached.SubscriptionId, cancellationToken);

        if (subscription == null)
        {
            return new SubscriptionKeyValidationResult(false, null, null, "Subscription not found", null, null, null, null);
        }

        var remainingDaily = subscription.DailyLimit - subscription.DailyUsed;
        var remainingMonthly = subscription.MonthlyLimit - subscription.MonthlyUsed;

        if (remainingDaily <= 0)
        {
            return new SubscriptionKeyValidationResult(false, null, null, "Daily notification limit exceeded", null, null, null, null);
        }

        if (remainingMonthly <= 0)
        {
            return new SubscriptionKeyValidationResult(false, null, null, "Monthly notification limit exceeded", null, null, null, null);
        }

        return new SubscriptionKeyValidationResult(
            true,
            cached.UserId,
            cached.SubscriptionId,
            null,
            cached.AllowSms,
            cached.AllowEmail,
            remainingDaily,
            remainingMonthly
        );
    }

    private async Task<SubscriptionKeyValidationResult> ValidateSubscriptionAsync(
        Subscription subscription,
        CancellationToken cancellationToken)
    {
        if (subscription.Status != SubscriptionStatus.Active)
        {
            _logger.LogWarning("Subscription {Id} is not active. Status: {Status}", subscription.Id, subscription.Status);
            return new SubscriptionKeyValidationResult(false, null, null, $"Subscription is {subscription.Status}", null, null, null, null);
        }

        if (subscription.ExpiresAt < DateTime.UtcNow)
        {
            _logger.LogWarning("Subscription {Id} has expired", subscription.Id);
            return new SubscriptionKeyValidationResult(false, null, null, "Subscription has expired", null, null, null, null);
        }

        if (!subscription.User.IsActive)
        {
            _logger.LogWarning("User {UserId} is not active", subscription.UserId);
            return new SubscriptionKeyValidationResult(false, null, null, "User account is inactive", null, null, null, null);
        }

        // Check and reset daily/monthly counters if needed
        await ResetCountersIfNeededAsync(subscription.Id, cancellationToken);

        // Re-fetch to get updated counters
        subscription = await _unitOfWork.GetRepository<Subscription>()
            .QueryNoTracking()
            .FirstOrDefaultAsync(s => s.Id == subscription.Id, cancellationToken);

        var remainingDaily = subscription!.DailyLimit - subscription.DailyUsed;
        var remainingMonthly = subscription.MonthlyLimit - subscription.MonthlyUsed;

        if (remainingDaily <= 0)
        {
            _logger.LogWarning("Subscription {Id} has exceeded daily limit", subscription.Id);
            return new SubscriptionKeyValidationResult(false, null, null, "Daily notification limit exceeded", null, null, null, null);
        }

        if (remainingMonthly <= 0)
        {
            _logger.LogWarning("Subscription {Id} has exceeded monthly limit", subscription.Id);
            return new SubscriptionKeyValidationResult(false, null, null, "Monthly notification limit exceeded", null, null, null, null);
        }

        _logger.LogDebug("Subscription {Id} validated successfully", subscription.Id);

        return new SubscriptionKeyValidationResult(
            true,
            subscription.UserId,
            subscription.Id,
            null,
            subscription.AllowSms,
            subscription.AllowEmail,
            remainingDaily,
            remainingMonthly
        );
    }

    public async Task<bool> CanSendNotificationAsync(
        Guid subscriptionId,
        NotificationType type,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _unitOfWork.GetRepository<Subscription>().GetByIdAsync(subscriptionId, cancellationToken);
        if (subscription == null) return false;

        return type switch
        {
            NotificationType.Email => subscription.AllowEmail,
            NotificationType.Sms => subscription.AllowSms,
            _ => false
        };
    }

    public async Task IncrementUsageAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < MaxConcurrencyRetries; attempt++)
        {
            try
            {
                var subscription = await _unitOfWork.GetRepository<Subscription>().GetByIdAsync(subscriptionId, cancellationToken);
                if (subscription == null) return;

                await ResetCountersIfNeededInternalAsync(subscription);

                subscription.DailyUsed++;
                subscription.MonthlyUsed++;

                await _unitOfWork.GetRepository<Subscription>().UpdateAsync(subscription, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogDebug(
                    "Incremented usage for subscription {Id}. Daily: {Daily}/{DailyLimit}, Monthly: {Monthly}/{MonthlyLimit}",
                    subscriptionId, subscription.DailyUsed, subscription.DailyLimit, subscription.MonthlyUsed, subscription.MonthlyLimit);
                
                return;
            }
            catch (AppDbConcurrencyException ex)
            {
                _logger.LogWarning(
                    "Concurrency conflict incrementing usage for subscription {Id}, attempt {Attempt}/{MaxRetries}: {Message}",
                    subscriptionId, attempt + 1, MaxConcurrencyRetries, ex.Message);

                if (attempt == MaxConcurrencyRetries - 1)
                {
                    _logger.LogError("Failed to increment usage after {MaxRetries} attempts", MaxConcurrencyRetries);
                    throw;
                }

                // Small delay before retry
                await Task.Delay(TimeSpan.FromMilliseconds(50 * (attempt + 1)), cancellationToken);
            }
        }
    }

    private async Task ResetCountersIfNeededAsync(Guid subscriptionId, CancellationToken cancellationToken)
    {
        var subscription = await _unitOfWork.GetRepository<Subscription>().GetByIdAsync(subscriptionId, cancellationToken);
        if (subscription == null) return;

        if (await ResetCountersIfNeededInternalAsync(subscription))
        {
            try
            {
                await _unitOfWork.GetRepository<Subscription>().UpdateAsync(subscription, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (AppDbConcurrencyException)
            {
                // Another process may have reset - that's fine
                _logger.LogDebug("Concurrency conflict during counter reset - likely already reset by another process");
            }
        }
    }

    private Task<bool> ResetCountersIfNeededInternalAsync(Subscription subscription)
    {
        var today = DateTime.UtcNow.Date;
        var firstOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var needsUpdate = false;

        if (subscription.LastResetDaily.Date < today)
        {
            subscription.DailyUsed = 0;
            subscription.LastResetDaily = today;
            needsUpdate = true;
            _logger.LogDebug("Reset daily counter for subscription {Id}", subscription.Id);
        }

        if (subscription.LastResetMonthly < firstOfMonth)
        {
            subscription.MonthlyUsed = 0;
            subscription.LastResetMonthly = firstOfMonth;
            needsUpdate = true;
            _logger.LogDebug("Reset monthly counter for subscription {Id}", subscription.Id);
        }

        return Task.FromResult(needsUpdate);
    }

    /// <summary>
    /// Invalidates the cache for a subscription (call when subscription is updated).
    /// </summary>
    public void InvalidateCache(string subscriptionKey)
    {
        var cacheKey = $"subscription:{subscriptionKey}";
        _cache.Remove(cacheKey);
        _logger.LogDebug("Invalidated cache for subscription key {Key}", subscriptionKey[..Math.Min(10, subscriptionKey.Length)] + "***");
    }

    private record SubscriptionCacheEntry(
        Guid SubscriptionId,
        Guid UserId,
        SubscriptionStatus Status,
        DateTime ExpiresAt,
        bool AllowSms,
        bool AllowEmail,
        bool UserIsActive
    );
}
