using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NotificationService.Application.DTOs;
using NotificationService.Application.Services;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Interfaces;
using NotificationService.Tests.Helpers;

namespace NotificationService.Tests.Application.Services;

public class SubscriptionValidationServiceTests
{
    private readonly Mock<IRepository<Subscription>> _subscriptionRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<SubscriptionValidationService>> _loggerMock;
    private readonly SubscriptionValidationService _service;

    public SubscriptionValidationServiceTests()
    {
        _subscriptionRepositoryMock = new Mock<IRepository<Subscription>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<SubscriptionValidationService>>();

        _service = new SubscriptionValidationService(
            _subscriptionRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task ValidateSubscriptionKeyAsync_WithEmptyKey_ReturnsInvalid()
    {
        // Act
        var result = await _service.ValidateSubscriptionKeyAsync("");

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be("Subscription key is required");
    }

    [Fact]
    public async Task CanSendNotificationAsync_WithValidSubscription_ReturnsTrue()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var subscription = TestDataFactory.CreateTestSubscription(
            id: subscriptionId,
            allowEmail: true,
            allowSms: true,
            status: SubscriptionStatus.Active
        );

        _subscriptionRepositoryMock
            .Setup(x => x.GetByIdAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        // Act
        var result = await _service.CanSendNotificationAsync(subscriptionId, NotificationType.Email);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanSendNotificationAsync_WithEmailNotAllowed_ReturnsFalse()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var subscription = TestDataFactory.CreateTestSubscription(
            id: subscriptionId,
            allowEmail: false,
            allowSms: true
        );

        _subscriptionRepositoryMock
            .Setup(x => x.GetByIdAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        // Act
        var result = await _service.CanSendNotificationAsync(subscriptionId, NotificationType.Email);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IncrementUsageAsync_IncrementsCounters()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var subscription = TestDataFactory.CreateTestSubscription(id: subscriptionId);
        var initialDaily = subscription.DailyUsed;
        var initialMonthly = subscription.MonthlyUsed;

        _subscriptionRepositoryMock
            .Setup(x => x.GetByIdAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _unitOfWorkMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _service.IncrementUsageAsync(subscriptionId);

        // Assert
        subscription.DailyUsed.Should().Be(initialDaily + 1);
        subscription.MonthlyUsed.Should().Be(initialMonthly + 1);
        _subscriptionRepositoryMock.Verify(x => x.UpdateAsync(subscription, It.IsAny<CancellationToken>()), Times.Once);
    }
}
