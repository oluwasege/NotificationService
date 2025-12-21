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

public class SubscriptionServiceTests
{
    private readonly Mock<IRepository<Subscription>> _subscriptionRepositoryMock;
    private readonly Mock<IRepository<User>> _userRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<SubscriptionService>> _loggerMock;
    private readonly SubscriptionService _service;

    public SubscriptionServiceTests()
    {
        _subscriptionRepositoryMock = new Mock<IRepository<Subscription>>();
        _userRepositoryMock = new Mock<IRepository<User>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<SubscriptionService>>();

        _service = new SubscriptionService(
            _subscriptionRepositoryMock.Object,
            _userRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task CreateSubscriptionAsync_WithValidData_CreatesSubscriptionSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = TestDataFactory.CreateTestUser(id: userId);
        var request = new CreateSubscriptionRequest(
            userId,
            "Test Subscription",
            365,
            1000,
            30000,
            true,
            true
        );

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _subscriptionRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription s, CancellationToken ct) => s);

        _unitOfWorkMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.CreateSubscriptionAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be(request.Name);
        result.SubscriptionKey.Should().NotBeNullOrEmpty();
        result.DailyLimit.Should().Be(request.DailyLimit);
        result.MonthlyLimit.Should().Be(request.MonthlyLimit);

        _subscriptionRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateSubscriptionAsync_WithInvalidUserId_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreateSubscriptionRequest(
            userId,
            "Test Subscription",
            365,
            1000,
            30000,
            true,
            true
        );

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateSubscriptionAsync(request));

        _subscriptionRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateSubscriptionAsync_WithValidData_UpdatesSuccessfully()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var subscription = TestDataFactory.CreateTestSubscription(id: subscriptionId);
        var request = new UpdateSubscriptionRequest(
            Name: "Updated Name",
            Status: SubscriptionStatus.Active,
            DailyLimit: 2000,
            MonthlyLimit: 60000,
            ExpiresAt: null,
            AllowSms: true,
            AllowEmail: true
        );

        _subscriptionRepositoryMock
            .Setup(x => x.GetByIdAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _unitOfWorkMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.UpdateSubscriptionAsync(subscriptionId, request);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Updated Name");
        result.DailyLimit.Should().Be(2000);

        _subscriptionRepositoryMock.Verify(x => x.UpdateAsync(subscription, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateSubscriptionAsync_WithNonExistentSubscription_ReturnsNull()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var request = new UpdateSubscriptionRequest();

        _subscriptionRepositoryMock
            .Setup(x => x.GetByIdAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);

        // Act
        var result = await _service.UpdateSubscriptionAsync(subscriptionId, request);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RegenerateKeyAsync_WithValidSubscription_RegeneratesKey()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var subscription = TestDataFactory.CreateTestSubscription(id: subscriptionId);
        var originalKey = subscription.SubscriptionKey;

        _subscriptionRepositoryMock
            .Setup(x => x.GetByIdAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _unitOfWorkMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.RegenerateKeyAsync(subscriptionId);

        // Assert
        result.Should().NotBeNull();
        result!.NewSubscriptionKey.Should().NotBeNullOrEmpty();
        result.NewSubscriptionKey.Should().NotBe(originalKey);

        _subscriptionRepositoryMock.Verify(x => x.UpdateAsync(subscription, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteSubscriptionAsync_WithExistingSubscription_DeletesSuccessfully()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var subscription = TestDataFactory.CreateTestSubscription(id: subscriptionId);

        _subscriptionRepositoryMock
            .Setup(x => x.GetByIdAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _unitOfWorkMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.DeleteSubscriptionAsync(subscriptionId);

        // Assert
        result.Should().BeTrue();
        _subscriptionRepositoryMock.Verify(x => x.SoftDeleteAsync(subscription, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteSubscriptionAsync_WithNonExistentSubscription_ReturnsFalse()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();

        _subscriptionRepositoryMock
            .Setup(x => x.GetByIdAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);

        // Act
        var result = await _service.DeleteSubscriptionAsync(subscriptionId);

        // Assert
        result.Should().BeFalse();
    }
}
