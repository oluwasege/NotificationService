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

public class DashboardServiceTests
{
    private readonly INotificationQueue _notificationQueue;
    private readonly ILogger<DashboardService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly DashboardService _dashboardService;
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<Subscription> _subscriptionRepository;
    private readonly IRepository<Notification> _notificationRepository;

    public DashboardServiceTests()
    {
        _notificationQueue = A.Fake<INotificationQueue>();
        _logger = A.Fake<ILogger<DashboardService>>();
        _unitOfWork = A.Fake<IUnitOfWork>();
        
        _userRepository = A.Fake<IRepository<User>>();
        _subscriptionRepository = A.Fake<IRepository<Subscription>>();
        _notificationRepository = A.Fake<IRepository<Notification>>();

        A.CallTo(() => _unitOfWork.GetRepository<User>()).Returns(_userRepository);
        A.CallTo(() => _unitOfWork.GetRepository<Subscription>()).Returns(_subscriptionRepository);
        A.CallTo(() => _unitOfWork.GetRepository<Notification>()).Returns(_notificationRepository);

        _dashboardService = new DashboardService(_notificationQueue, _logger, _unitOfWork);
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_ReturnsCorrectSummary()
    {
        // Arrange
        A.CallTo(() => _userRepository.CountAsync(A<Expression<Func<User, bool>>>._, A<CancellationToken>._)).Returns(10);
        A.CallTo(() => _subscriptionRepository.CountAsync(A<Expression<Func<Subscription, bool>>>._, A<CancellationToken>._)).Returns(5);
        
        // Mock notification counts
        A.CallTo(() => _notificationRepository.QueryNoTracking())
            .Returns(MockAsyncQueryable.Build(new List<Notification>()));

        A.CallTo(() => _notificationRepository.GetAllQueryable(A<Expression<Func<Notification, bool>>>._))
            .Returns(MockAsyncQueryable.Build(new List<Notification>()));
        
        // Act
        var result = await _dashboardService.GetDashboardSummaryAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result.TotalUsers);
        Assert.Equal(5, result.TotalSubscriptions);
    }

    [Fact]
    public async Task GetNotificationStatsAsync_ReturnsStatsForDateRange()
    {
        // Arrange
        var fromDate = DateTime.UtcNow.AddDays(-2);
        var toDate = DateTime.UtcNow;
        
        var notifications = new List<Notification>
        {
            new() { CreatedAt = DateTime.UtcNow, Status = NotificationStatus.Sent, Type = NotificationType.Email },
            new() { CreatedAt = DateTime.UtcNow.AddDays(-1), Status = NotificationStatus.Failed, Type = NotificationType.Sms }
        };

        A.CallTo(() => _notificationRepository.GetAllQueryable(A<Expression<Func<Notification, bool>>>._))
            .Returns(MockAsyncQueryable.Build(notifications));

        // Act
        var result = await _dashboardService.GetNotificationStatsAsync(fromDate, toDate);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Equal((toDate.Date - fromDate.Date).Days + 1, result.Count);
    }
}
