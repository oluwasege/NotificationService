using FakeItEasy;
using Microsoft.Extensions.Logging;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Services;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Interfaces;
using NotificationService.Tests.Helpers;
using System.Linq.Expressions;

namespace NotificationService.Tests.Services;

public class NotificationAppServiceTests
{
    private readonly INotificationQueue _notificationQueue;
    private readonly ISubscriptionValidationService _subscriptionValidation;
    private readonly ITemplateService _templateService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<NotificationAppService> _logger;
    private readonly NotificationAppService _notificationService;
    private readonly IRepository<Notification> _notificationRepository;
    private readonly IRepository<NotificationLog> _notificationLogRepository;
    private readonly IRepository<OutboxMessage> _outboxRepository;

    public NotificationAppServiceTests()
    {
        _notificationQueue = A.Fake<INotificationQueue>();
        _subscriptionValidation = A.Fake<ISubscriptionValidationService>();
        _templateService = A.Fake<ITemplateService>();
        _unitOfWork = A.Fake<IUnitOfWork>();
        _logger = A.Fake<ILogger<NotificationAppService>>();
        
        _notificationRepository = A.Fake<IRepository<Notification>>();
        _notificationLogRepository = A.Fake<IRepository<NotificationLog>>();
        _outboxRepository = A.Fake<IRepository<OutboxMessage>>();

        A.CallTo(() => _unitOfWork.GetRepository<Notification>()).Returns(_notificationRepository);
        A.CallTo(() => _unitOfWork.GetRepository<NotificationLog>()).Returns(_notificationLogRepository);
        A.CallTo(() => _unitOfWork.GetRepository<OutboxMessage>()).Returns(_outboxRepository);

        _notificationService = new NotificationAppService(
            _notificationQueue,
            _subscriptionValidation,
            _templateService,
            _unitOfWork,
            _logger);
    }

    [Fact]
    public async Task SendNotificationAsync_WithValidRequest_ReturnsSuccess()
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

        A.CallTo(() => _subscriptionValidation.CanSendNotificationAsync(
            subscriptionId, request.Type, A<CancellationToken>._))
            .Returns(true);

        A.CallTo(() => _notificationRepository.AddAsync(A<Notification>._, A<CancellationToken>._))
            .Invokes((Notification n, CancellationToken _) => n.Id = Guid.NewGuid());

        // Act
        var result = await _notificationService.SendNotificationAsync(userId, subscriptionId, request);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.NotificationId);
        Assert.Equal(NotificationStatus.Processing, result.Status);

        A.CallTo(() => _notificationRepository.AddAsync(A<Notification>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _notificationQueue.EnqueueAsync(A<Notification>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _subscriptionValidation.IncrementUsageAsync(subscriptionId, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task SendNotificationAsync_WithInvalidSubscription_ThrowsException()
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

        A.CallTo(() => _subscriptionValidation.CanSendNotificationAsync(
            subscriptionId, request.Type, A<CancellationToken>._))
            .Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _notificationService.SendNotificationAsync(userId, subscriptionId, request));
    }

    [Fact]
    public async Task GetNotificationByIdAsync_WithValidId_ReturnsNotification()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var notification = new Notification
        {
            Id = notificationId,
            Type = NotificationType.Email,
            Status = NotificationStatus.Sent,
            Recipient = "test@example.com",
            Subject = "Test",
            Body = "Body",
            Logs = new List<NotificationLog>()
        };

        A.CallTo(() => _notificationRepository.QueryNoTracking())
            .Returns(MockAsyncQueryable.Build(new List<Notification> { notification }));

        A.CallTo(() => _notificationRepository.FirstOrDefaultAsync(
            A<Expression<Func<Notification, bool>>>._, A<CancellationToken>._))
            .Returns(notification);

        // Act
        var result = await _notificationService.GetNotificationByIdAsync(notificationId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(notificationId, result.Id);
        Assert.Equal(notification.Recipient, result.Recipient);
    }

    [Fact]
    public async Task CancelNotificationAsync_WithPendingNotification_ReturnsTrue()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var notification = new Notification
        {
            Id = notificationId,
            Status = NotificationStatus.Pending
        };

        A.CallTo(() => _notificationRepository.GetByIdAsync(notificationId, A<CancellationToken>._))
            .Returns(notification);

        // Act
        var result = await _notificationService.CancelNotificationAsync(notificationId);

        // Assert
        Assert.True(result);
        Assert.Equal(NotificationStatus.Failed, notification.Status);
        Assert.Equal("Cancelled by user", notification.ErrorMessage);
        
        A.CallTo(() => _notificationRepository.UpdateAsync(notification, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task CancelNotificationAsync_WithNonPendingNotification_ReturnsFalse()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var notification = new Notification
        {
            Id = notificationId,
            Status = NotificationStatus.Sent
        };

        A.CallTo(() => _notificationRepository.GetByIdAsync(notificationId, A<CancellationToken>._))
            .Returns(notification);

        // Act
        var result = await _notificationService.CancelNotificationAsync(notificationId);

        // Assert
        Assert.False(result);
        Assert.Equal(NotificationStatus.Sent, notification.Status);
    }
}
