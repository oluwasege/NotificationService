using FakeItEasy;
using Microsoft.Extensions.Logging;
using NotificationService.Application.DTOs;
using NotificationService.Application.Services;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Interfaces;
using NotificationService.Tests.Helpers;
using System.Linq.Expressions;

namespace NotificationService.Tests.Services;

public class SubscriptionValidationServiceTests
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SubscriptionValidationService> _logger;
    private readonly SubscriptionValidationService _validationService;
    private readonly IRepository<Subscription> _subscriptionRepository;

    public SubscriptionValidationServiceTests()
    {
        _unitOfWork = A.Fake<IUnitOfWork>();
        _logger = A.Fake<ILogger<SubscriptionValidationService>>();
        _subscriptionRepository = A.Fake<IRepository<Subscription>>();

        A.CallTo(() => _unitOfWork.GetRepository<Subscription>()).Returns(_subscriptionRepository);

        _validationService = new SubscriptionValidationService(_unitOfWork, _logger);
    }

    [Fact]
    public async Task ValidateSubscriptionKeyAsync_WithValidKey_ReturnsSuccess()
    {
        // Arrange
        var key = "sk_live_validkey";
        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            SubscriptionKey = key,
            Status = SubscriptionStatus.Active,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            DailyLimit = 100,
            DailyUsed = 0,
            MonthlyLimit = 1000,
            MonthlyUsed = 0,
            User = new User { IsActive = true },
            LastResetDaily = DateTime.UtcNow.Date,
            LastResetMonthly = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        A.CallTo(() => _subscriptionRepository.QueryNoTracking())
            .Returns(MockAsyncQueryable.Build(new List<Subscription> { subscription }));

        A.CallTo(() => _subscriptionRepository.FirstOrDefaultAsync(
            A<Expression<Func<Subscription, bool>>>._, A<CancellationToken>._))
            .Returns(subscription);

        // Act
        var result = await _validationService.ValidateSubscriptionKeyAsync(key);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(subscription.Id, result.SubscriptionId);
        Assert.Equal(subscription.UserId, result.UserId);
    }

    [Fact]
    public async Task ValidateSubscriptionKeyAsync_WithExpiredSubscription_ReturnsFailure()
    {
        // Arrange
        var key = "sk_live_expiredkey";
        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            SubscriptionKey = key,
            Status = SubscriptionStatus.Active,
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
            User = new User { IsActive = true }
        };

        A.CallTo(() => _subscriptionRepository.QueryNoTracking())
            .Returns(MockAsyncQueryable.Build(new List<Subscription> { subscription }));

        A.CallTo(() => _subscriptionRepository.FirstOrDefaultAsync(
            A<Expression<Func<Subscription, bool>>>._, A<CancellationToken>._))
            .Returns(subscription);

        // Act
        var result = await _validationService.ValidateSubscriptionKeyAsync(key);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Subscription has expired", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateSubscriptionKeyAsync_WithExceededDailyLimit_ReturnsFailure()
    {
        // Arrange
        var key = "sk_live_limitkey";
        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            SubscriptionKey = key,
            Status = SubscriptionStatus.Active,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            DailyLimit = 100,
            DailyUsed = 100,
            MonthlyLimit = 1000,
            MonthlyUsed = 100,
            User = new User { IsActive = true },
            LastResetDaily = DateTime.UtcNow.Date,
            LastResetMonthly = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        A.CallTo(() => _subscriptionRepository.QueryNoTracking())
            .Returns(MockAsyncQueryable.Build(new List<Subscription> { subscription }));

        A.CallTo(() => _subscriptionRepository.FirstOrDefaultAsync(
            A<Expression<Func<Subscription, bool>>>._, A<CancellationToken>._))
            .Returns(subscription);

        // Act
        var result = await _validationService.ValidateSubscriptionKeyAsync(key);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Daily notification limit exceeded", result.ErrorMessage);
    }

    [Fact]
    public async Task CanSendNotificationAsync_WithAllowedType_ReturnsTrue()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var subscription = new Subscription
        {
            Id = subscriptionId,
            AllowEmail = true,
            AllowSms = false
        };

        A.CallTo(() => _subscriptionRepository.GetByIdAsync(subscriptionId, A<CancellationToken>._))
            .Returns(subscription);

        // Act
        var result = await _validationService.CanSendNotificationAsync(subscriptionId, NotificationType.Email);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task CanSendNotificationAsync_WithDisallowedType_ReturnsFalse()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var subscription = new Subscription
        {
            Id = subscriptionId,
            AllowEmail = true,
            AllowSms = false
        };

        A.CallTo(() => _subscriptionRepository.GetByIdAsync(subscriptionId, A<CancellationToken>._))
            .Returns(subscription);

        // Act
        var result = await _validationService.CanSendNotificationAsync(subscriptionId, NotificationType.Sms);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IncrementUsageAsync_IncrementsCounters()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var subscription = new Subscription
        {
            Id = subscriptionId,
            DailyUsed = 10,
            MonthlyUsed = 100,
            LastResetDaily = DateTime.UtcNow.Date,
            LastResetMonthly = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        A.CallTo(() => _subscriptionRepository.GetByIdAsync(subscriptionId, A<CancellationToken>._))
            .Returns(subscription);

        // Act
        await _validationService.IncrementUsageAsync(subscriptionId);

        // Assert
        Assert.Equal(11, subscription.DailyUsed);
        Assert.Equal(101, subscription.MonthlyUsed);
        
        A.CallTo(() => _subscriptionRepository.UpdateAsync(subscription, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }
}
