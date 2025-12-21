using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Services;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Interfaces;
using NotificationService.Tests.Helpers;

namespace NotificationService.Tests.Application.Services;

public class NotificationAppServiceTests
{
    private readonly Mock<IRepository<Notification>> _notificationRepositoryMock;
    private readonly Mock<IRepository<NotificationLog>> _logRepositoryMock;
    private readonly Mock<INotificationQueue> _notificationQueueMock;
    private readonly Mock<ISubscriptionValidationService> _subscriptionValidationMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<NotificationAppService>> _loggerMock;
    private readonly NotificationAppService _service;

    public NotificationAppServiceTests()
    {
        _notificationRepositoryMock = new Mock<IRepository<Notification>>();
        _logRepositoryMock = new Mock<IRepository<NotificationLog>>();
        _notificationQueueMock = new Mock<INotificationQueue>();
        _subscriptionValidationMock = new Mock<ISubscriptionValidationService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<NotificationAppService>>();

        _service = new NotificationAppService(
            _notificationRepositoryMock.Object,
            _logRepositoryMock.Object,
            _notificationQueueMock.Object,
            _subscriptionValidationMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task SendNotificationAsync_WithValidRequest_CreatesNotificationSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var request = TestDataFactory.CreateSendNotificationRequest();

        _subscriptionValidationMock
            .Setup(x => x.CanSendNotificationAsync(subscriptionId, request.Type, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _notificationRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Notification n, CancellationToken ct) => n);

        _logRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<NotificationLog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationLog l, CancellationToken ct) => l);

        _unitOfWorkMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.SendNotificationAsync(userId, subscriptionId, request);

        // Assert
        result.Should().NotBeNull();
        result.NotificationId.Should().NotBeEmpty();
        result.Status.Should().Be(NotificationStatus.Processing);
        result.Message.Should().Be("Notification created successfully");

        _notificationRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Once);
        _logRepositoryMock.Verify(x => x.AddAsync(It.IsAny<NotificationLog>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _subscriptionValidationMock.Verify(x => x.IncrementUsageAsync(subscriptionId, It.IsAny<CancellationToken>()), Times.Once);
        _notificationQueueMock.Verify(x => x.EnqueueAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendNotificationAsync_WhenNotificationTypeNotAllowed_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var request = TestDataFactory.CreateSendNotificationRequest(type: NotificationType.Sms);

        _subscriptionValidationMock
            .Setup(x => x.CanSendNotificationAsync(subscriptionId, request.Type, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SendNotificationAsync(userId, subscriptionId, request));

        _notificationRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendNotificationAsync_WithScheduledTime_CreatesPendingNotification()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var futureTime = DateTime.UtcNow.AddHours(2);
        var request = new SendNotificationRequest(
            NotificationType.Email,
            "test@example.com",
            "Subject",
            "Body",
            NotificationPriority.Normal,
            futureTime
        );

        _subscriptionValidationMock
            .Setup(x => x.CanSendNotificationAsync(subscriptionId, request.Type, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.SendNotificationAsync(userId, subscriptionId, request);

        // Assert
        result.Status.Should().Be(NotificationStatus.Pending);
        _notificationQueueMock.Verify(x => x.EnqueueAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendBatchNotificationsAsync_WithValidRequests_ProcessesAllNotifications()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var notifications = new List<SendNotificationRequest>
        {
            TestDataFactory.CreateSendNotificationRequest(),
            TestDataFactory.CreateSendNotificationRequest(recipient: "test2@example.com")
        };
        var request = new SendBatchNotificationRequest(notifications);

        _subscriptionValidationMock
            .Setup(x => x.CanSendNotificationAsync(subscriptionId, It.IsAny<NotificationType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.SendBatchNotificationsAsync(userId, subscriptionId, request);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(2);
        result.AcceptedCount.Should().Be(2);
        result.RejectedCount.Should().Be(0);
        result.Results.Should().HaveCount(2);
        result.Results.Should().AllSatisfy(r => r.Accepted.Should().BeTrue());
    }

    [Fact]
    public async Task SendBatchNotificationsAsync_WithSomeFailures_ReturnsPartialSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var notifications = new List<SendNotificationRequest>
        {
            TestDataFactory.CreateSendNotificationRequest(),
            TestDataFactory.CreateSendNotificationRequest(type: NotificationType.Sms)
        };
        var request = new SendBatchNotificationRequest(notifications);

        _subscriptionValidationMock
            .Setup(x => x.CanSendNotificationAsync(subscriptionId, NotificationType.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _subscriptionValidationMock
            .Setup(x => x.CanSendNotificationAsync(subscriptionId, NotificationType.Sms, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.SendBatchNotificationsAsync(userId, subscriptionId, request);

        // Assert
        result.TotalCount.Should().Be(2);
        result.AcceptedCount.Should().Be(1);
        result.RejectedCount.Should().Be(1);
        result.Results.Should().Contain(r => r.Accepted == true);
        result.Results.Should().Contain(r => r.Accepted == false);
    }

    [Fact]
    public async Task CancelNotificationAsync_WithPendingNotification_CancelsSuccessfully()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var notification = TestDataFactory.CreateTestNotification(
            id: notificationId,
            status: NotificationStatus.Pending
        );

        _notificationRepositoryMock
            .Setup(x => x.GetByIdAsync(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);

        // Act
        var result = await _service.CancelNotificationAsync(notificationId);

        // Assert
        result.Should().BeTrue();
        notification.Status.Should().Be(NotificationStatus.Failed);
        notification.ErrorMessage.Should().Be("Cancelled by user");
        _notificationRepositoryMock.Verify(x => x.UpdateAsync(notification, It.IsAny<CancellationToken>()), Times.Once);
        _logRepositoryMock.Verify(x => x.AddAsync(It.IsAny<NotificationLog>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelNotificationAsync_WithNonPendingNotification_ReturnsFalse()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var notification = TestDataFactory.CreateTestNotification(
            id: notificationId,
            status: NotificationStatus.Delivered
        );

        _notificationRepositoryMock
            .Setup(x => x.GetByIdAsync(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);

        // Act
        var result = await _service.CancelNotificationAsync(notificationId);

        // Assert
        result.Should().BeFalse();
        _notificationRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelNotificationAsync_WithNonExistentNotification_ReturnsFalse()
    {
        // Arrange
        var notificationId = Guid.NewGuid();

        _notificationRepositoryMock
            .Setup(x => x.GetByIdAsync(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Notification?)null);

        // Act
        var result = await _service.CancelNotificationAsync(notificationId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RetryNotificationAsync_WithFailedNotification_RetriesSuccessfully()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var notification = TestDataFactory.CreateTestNotification(
            id: notificationId,
            status: NotificationStatus.Failed
        );

        _notificationRepositoryMock
            .Setup(x => x.GetByIdAsync(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);

        // Act
        var result = await _service.RetryNotificationAsync(notificationId);

        // Assert
        result.Should().BeTrue();
        notification.Status.Should().Be(NotificationStatus.Retrying);
        notification.RetryCount.Should().Be(1);
        notification.ErrorMessage.Should().BeNull();
        _notificationRepositoryMock.Verify(x => x.UpdateAsync(notification, It.IsAny<CancellationToken>()), Times.Once);
        _notificationQueueMock.Verify(x => x.EnqueueAsync(notification, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RetryNotificationAsync_WithNonFailedNotification_ReturnsFalse()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var notification = TestDataFactory.CreateTestNotification(
            id: notificationId,
            status: NotificationStatus.Delivered
        );

        _notificationRepositoryMock
            .Setup(x => x.GetByIdAsync(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);

        // Act
        var result = await _service.RetryNotificationAsync(notificationId);

        // Assert
        result.Should().BeFalse();
        _notificationRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RetryNotificationAsync_WithNonExistentNotification_ReturnsFalse()
    {
        // Arrange
        var notificationId = Guid.NewGuid();

        _notificationRepositoryMock
            .Setup(x => x.GetByIdAsync(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Notification?)null);

        // Act
        var result = await _service.RetryNotificationAsync(notificationId);

        // Assert
        result.Should().BeFalse();
    }
}
