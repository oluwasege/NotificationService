using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Interfaces;

namespace NotificationService.Application.Services;

public class SubscriptionValidationService : ISubscriptionValidationService
{
    private readonly IRepository<Subscription> _subscriptionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SubscriptionValidationService> _logger;

    public SubscriptionValidationService(
        IRepository<Subscription> subscriptionRepository,
        IUnitOfWork unitOfWork,
        ILogger<SubscriptionValidationService> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _unitOfWork = unitOfWork;
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

        var subscription = await _subscriptionRepository
            .QueryNoTracking()
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.SubscriptionKey == subscriptionKey, cancellationToken);

        if (subscription == null)
        {
            _logger.LogWarning("Invalid subscription key: {Key}", subscriptionKey[..Math.Min(10, subscriptionKey.Length)] + "***");
            return new SubscriptionKeyValidationResult(false, null, null, "Invalid subscription key", null, null, null, null);
        }

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
        await ResetCountersIfNeededAsync(subscription, cancellationToken);

        var remainingDaily = subscription.DailyLimit - subscription.DailyUsed;
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
        var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId, cancellationToken);
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
        var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId, cancellationToken);
        if (subscription == null) return;

        await ResetCountersIfNeededAsync(subscription, cancellationToken);

        subscription.DailyUsed++;
        subscription.MonthlyUsed++;

        await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Incremented usage for subscription {Id}. Daily: {Daily}/{DailyLimit}, Monthly: {Monthly}/{MonthlyLimit}",
            subscriptionId, subscription.DailyUsed, subscription.DailyLimit, subscription.MonthlyUsed, subscription.MonthlyLimit);
    }

    private async Task ResetCountersIfNeededAsync(Subscription subscription, CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var firstOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
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

        if (needsUpdate)
        {
            await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
