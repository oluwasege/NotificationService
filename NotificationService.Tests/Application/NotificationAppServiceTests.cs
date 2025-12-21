using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Services;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Interfaces;

namespace NotificationService.Tests.Application;

public class NotificationAppServiceTests
{
    private readonly Mock<IRepository<Notification>> _notificationRepoMock;
    private readonly Mock<IRepository<NotificationLog>> _logRepoMock;
    private readonly Mock<INotificationQueue> _queueMock;
    private readonly Mock<ISubscriptionValidationService> _subscriptionValidationMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<NotificationAppService>> _loggerMock;
    private readonly NotificationAppService _service;

    public NotificationAppServiceTests()
    {
        _notificationRepoMock = new Mock<IRepository<Notification>>();
        _logRepoMock = new Mock<IRepository<NotificationLog>>();
        _queueMock = new Mock<INotificationQueue>();
        _subscriptionValidationMock = new Mock<ISubscriptionValidationService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<NotificationAppService>>();

        _service = new NotificationAppService(
            _notificationRepoMock.Object,
            _logRepoMock.Object,
            _queueMock.Object,
            _subscriptionValidationMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task SendNotificationAsync_ValidRequest_ReturnsSuccessResponse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var request = new SendNotificationRequest(
            NotificationType.Email,
            "test@example.com",
            "Test Subject",
            "Test Body",
            NotificationPriority.Normal
        );

        _subscriptionValidationMock
            .Setup(x => x.CanSendNotificationAsync(subscriptionId, request.Type, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.SendNotificationAsync(userId, subscriptionId, request);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(NotificationStatus.Processing);
        result.Message.Should().Be("Notification created successfully");

        _notificationRepoMock.Verify(x => x.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Once);
        _logRepoMock.Verify(x => x.AddAsync(It.IsAny<NotificationLog>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _subscriptionValidationMock.Verify(x => x.IncrementUsageAsync(subscriptionId, It.IsAny<CancellationToken>()), Times.Once);
        _queueMock.Verify(x => x.EnqueueAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendNotificationAsync_NotificationTypeNotAllowed_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var request = new SendNotificationRequest(
            NotificationType.Sms,
            "+1234567890",
            "Test",
            "Test Body",
            NotificationPriority.Normal
        );

        _subscriptionValidationMock
            .Setup(x => x.CanSendNotificationAsync(subscriptionId, request.Type, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.SendNotificationAsync(userId, subscriptionId, request));
    }

    [Fact]
    public async Task SendNotificationAsync_ScheduledNotification_SetsPendingStatus()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var scheduledTime = DateTime.UtcNow.AddHours(1);
        var request = new SendNotificationRequest(
            NotificationType.Email,
            "test@example.com",
            "Test Subject",
            "Test Body",
            NotificationPriority.Normal,
            scheduledTime
        );

        Notification? capturedNotification = null;
        _notificationRepoMock
            .Setup(x => x.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((n, ct) => capturedNotification = n)
            .ReturnsAsync((Notification n, CancellationToken ct) => n);

        _subscriptionValidationMock
            .Setup(x => x.CanSendNotificationAsync(subscriptionId, request.Type, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.SendNotificationAsync(userId, subscriptionId, request);

        // Assert
        result.Status.Should().Be(NotificationStatus.Pending);
        capturedNotification.Should().NotBeNull();
        capturedNotification!.Status.Should().Be(NotificationStatus.Pending);
        _queueMock.Verify(x => x.EnqueueAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendBatchNotificationsAsync_ValidBatch_ReturnsSuccessResponse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var request = new SendBatchNotificationRequest(
            new List<SendNotificationRequest>
            {
                new(NotificationType.Email, "test1@example.com", "Test 1", "Body 1"),
                new(NotificationType.Email, "test2@example.com", "Test 2", "Body 2")
            }
        );

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
        result.Results.All(r => r.Accepted).Should().BeTrue();
    }

    [Fact]
    public async Task SendBatchNotificationsAsync_PartialFailure_ReturnsCorrectCounts()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var request = new SendBatchNotificationRequest(
            new List<SendNotificationRequest>
            {
                new(NotificationType.Email, "test1@example.com", "Test 1", "Body 1"),
                new(NotificationType.Sms, "test2@example.com", "Test 2", "Body 2")
            }
        );

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
        result.Results.Count(r => r.Accepted).Should().Be(1);
        result.Results.Count(r => !r.Accepted).Should().Be(1);
    }

    [Fact]
    public async Task CancelNotificationAsync_PendingNotification_ReturnsTrue()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var notification = new Notification
        {
            Id = notificationId,
            Status = NotificationStatus.Pending
        };

        _notificationRepoMock
            .Setup(x => x.GetByIdAsync(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);

        // Act
        var result = await _service.CancelNotificationAsync(notificationId);

        // Assert
        result.Should().BeTrue();
        notification.Status.Should().Be(NotificationStatus.Failed);
        notification.ErrorMessage.Should().Be("Cancelled by user");
        _notificationRepoMock.Verify(x => x.UpdateAsync(notification, It.IsAny<CancellationToken>()), Times.Once);
        _logRepoMock.Verify(x => x.AddAsync(It.IsAny<NotificationLog>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelNotificationAsync_NotPendingNotification_ReturnsFalse()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var notification = new Notification
        {
            Id = notificationId,
            Status = NotificationStatus.Delivered
        };

        _notificationRepoMock
            .Setup(x => x.GetByIdAsync(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);

        // Act
        var result = await _service.CancelNotificationAsync(notificationId);

        // Assert
        result.Should().BeFalse();
        _notificationRepoMock.Verify(x => x.UpdateAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RetryNotificationAsync_FailedNotification_ReturnsTrue()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var notification = new Notification
        {
            Id = notificationId,
            Status = NotificationStatus.Failed,
            RetryCount = 1
        };

        _notificationRepoMock
            .Setup(x => x.GetByIdAsync(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);

        // Act
        var result = await _service.RetryNotificationAsync(notificationId);

        // Assert
        result.Should().BeTrue();
        notification.Status.Should().Be(NotificationStatus.Retrying);
        notification.RetryCount.Should().Be(2);
        notification.ErrorMessage.Should().BeNull();
        _queueMock.Verify(x => x.EnqueueAsync(notification, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RetryNotificationAsync_NotificationNotFound_ReturnsFalse()
    {
        // Arrange
        var notificationId = Guid.NewGuid();

        _notificationRepoMock
            .Setup(x => x.GetByIdAsync(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Notification?)null);

        // Act
        var result = await _service.RetryNotificationAsync(notificationId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RetryNotificationAsync_NotFailedNotification_ReturnsFalse()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var notification = new Notification
        {
            Id = notificationId,
            Status = NotificationStatus.Delivered
        };

        _notificationRepoMock
            .Setup(x => x.GetByIdAsync(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);

        // Act
        var result = await _service.RetryNotificationAsync(notificationId);

        // Assert
        result.Should().BeFalse();
        _notificationRepoMock.Verify(x => x.UpdateAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
