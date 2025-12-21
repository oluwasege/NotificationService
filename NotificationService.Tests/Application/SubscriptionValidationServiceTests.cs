using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NotificationService.Application.Services;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Interfaces;

namespace NotificationService.Tests.Application;

public class SubscriptionValidationServiceTests
{
    private readonly Mock<IRepository<Subscription>> _subscriptionRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<SubscriptionValidationService>> _loggerMock;
    private readonly SubscriptionValidationService _service;

    public SubscriptionValidationServiceTests()
    {
        _subscriptionRepoMock = new Mock<IRepository<Subscription>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<SubscriptionValidationService>>();

        _service = new SubscriptionValidationService(
            _subscriptionRepoMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task CanSendNotificationAsync_EmailAllowed_ReturnsTrue()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var subscription = new Subscription
        {
            Id = subscriptionId,
            AllowEmail = true,
            AllowSms = false
        };

        _subscriptionRepoMock
            .Setup(x => x.GetByIdAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        // Act
        var result = await _service.CanSendNotificationAsync(subscriptionId, NotificationType.Email);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanSendNotificationAsync_EmailNotAllowed_ReturnsFalse()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var subscription = new Subscription
        {
            Id = subscriptionId,
            AllowEmail = false,
            AllowSms = true
        };

        _subscriptionRepoMock
            .Setup(x => x.GetByIdAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        // Act
        var result = await _service.CanSendNotificationAsync(subscriptionId, NotificationType.Email);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanSendNotificationAsync_SmsAllowed_ReturnsTrue()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var subscription = new Subscription
        {
            Id = subscriptionId,
            AllowEmail = false,
            AllowSms = true
        };

        _subscriptionRepoMock
            .Setup(x => x.GetByIdAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        // Act
        var result = await _service.CanSendNotificationAsync(subscriptionId, NotificationType.Sms);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanSendNotificationAsync_SubscriptionNotFound_ReturnsFalse()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();

        _subscriptionRepoMock
            .Setup(x => x.GetByIdAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);

        // Act
        var result = await _service.CanSendNotificationAsync(subscriptionId, NotificationType.Email);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IncrementUsageAsync_ValidSubscription_IncrementsCounters()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var subscription = new Subscription
        {
            Id = subscriptionId,
            DailyUsed = 10,
            MonthlyUsed = 100,
            LastResetDaily = DateTime.UtcNow,
            LastResetMonthly = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1)
        };

        _subscriptionRepoMock
            .Setup(x => x.GetByIdAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        // Act
        await _service.IncrementUsageAsync(subscriptionId);

        // Assert
        subscription.DailyUsed.Should().Be(11);
        subscription.MonthlyUsed.Should().Be(101);
        _subscriptionRepoMock.Verify(x => x.UpdateAsync(subscription, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IncrementUsageAsync_SubscriptionNotFound_DoesNothing()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();

        _subscriptionRepoMock
            .Setup(x => x.GetByIdAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);

        // Act
        await _service.IncrementUsageAsync(subscriptionId);

        // Assert
        _subscriptionRepoMock.Verify(x => x.UpdateAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
