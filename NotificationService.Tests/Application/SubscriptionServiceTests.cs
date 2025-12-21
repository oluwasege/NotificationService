using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NotificationService.Application.DTOs;
using NotificationService.Application.Services;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Interfaces;

namespace NotificationService.Tests.Application;

public class SubscriptionServiceTests
{
    private readonly Mock<IRepository<Subscription>> _subscriptionRepoMock;
    private readonly Mock<IRepository<User>> _userRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<SubscriptionService>> _loggerMock;
    private readonly SubscriptionService _service;

    public SubscriptionServiceTests()
    {
        _subscriptionRepoMock = new Mock<IRepository<Subscription>>();
        _userRepoMock = new Mock<IRepository<User>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<SubscriptionService>>();

        _service = new SubscriptionService(
            _subscriptionRepoMock.Object,
            _userRepoMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task GetSubscriptionByIdAsync_ExistingSubscription_ReturnsSubscriptionDto()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var subscription = new Subscription
        {
            Id = subscriptionId,
            Name = "Test Subscription",
            SubscriptionKey = "sk_test_12345678901234567890",
            Status = SubscriptionStatus.Active,
            DailyLimit = 1000,
            MonthlyLimit = 30000
        };

        _subscriptionRepoMock
            .Setup(x => x.GetByIdAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        // Act
        var result = await _service.GetSubscriptionByIdAsync(subscriptionId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(subscriptionId);
        result.Name.Should().Be("Test Subscription");
        result.Status.Should().Be(SubscriptionStatus.Active);
        result.DailyLimit.Should().Be(1000);
        result.MonthlyLimit.Should().Be(30000);
        // Verify key is masked
        result.SubscriptionKey.Should().NotBe(subscription.SubscriptionKey);
        result.SubscriptionKey.Should().Contain("*");
    }

    [Fact]
    public async Task GetSubscriptionByIdAsync_NonExistingSubscription_ReturnsNull()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();

        _subscriptionRepoMock
            .Setup(x => x.GetByIdAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);

        // Act
        var result = await _service.GetSubscriptionByIdAsync(subscriptionId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteSubscriptionAsync_ExistingSubscription_ReturnsTrue()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var subscription = new Subscription { Id = subscriptionId };

        _subscriptionRepoMock
            .Setup(x => x.GetByIdAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        // Act
        var result = await _service.DeleteSubscriptionAsync(subscriptionId);

        // Assert
        result.Should().BeTrue();
        _subscriptionRepoMock.Verify(x => x.SoftDeleteAsync(subscription, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteSubscriptionAsync_NonExistingSubscription_ReturnsFalse()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();

        _subscriptionRepoMock
            .Setup(x => x.GetByIdAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);

        // Act
        var result = await _service.DeleteSubscriptionAsync(subscriptionId);

        // Assert
        result.Should().BeFalse();
        _subscriptionRepoMock.Verify(x => x.SoftDeleteAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
