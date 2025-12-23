using FakeItEasy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Interfaces;
using NotificationService.Infrastructure.BackgroundServices;
using NotificationService.Infrastructure.Data;
using NotificationService.Infrastructure.Providers;

namespace NotificationService.Tests.BackgroundServices;

public class NotificationProcessorServiceTests
{
    private readonly IServiceProvider _rootProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IServiceScope _scope;
    private readonly IServiceProvider _scopedProvider;
    private readonly INotificationQueue _notificationQueue;
    private readonly ILogger<NotificationProcessorService> _logger;
    private readonly NotificationDbContext _dbContext;
    private readonly NotificationProviderFactory _providerFactory;
    private readonly IWebhookService _webhookService;
    private readonly INotificationProvider _notificationProvider;

    public NotificationProcessorServiceTests()
    {
        _rootProvider = A.Fake<IServiceProvider>();
        _scopeFactory = A.Fake<IServiceScopeFactory>();
        _scope = A.Fake<IServiceScope>();
        _scopedProvider = A.Fake<IServiceProvider>();
        _notificationQueue = A.Fake<INotificationQueue>();
        _logger = A.Fake<ILogger<NotificationProcessorService>>();

        // Setup DbContext
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new NotificationDbContext(options);

        // Setup Provider Factory and Provider
        _notificationProvider = A.Fake<INotificationProvider>();

        // Use real factory with mocked service provider
        _providerFactory = new NotificationProviderFactory(_scopedProvider);
        _webhookService = A.Fake<IWebhookService>();

        // Setup Service Provider Chain
        A.CallTo(() => _rootProvider.GetService(typeof(IServiceScopeFactory))).Returns(_scopeFactory);
        A.CallTo(() => _scopeFactory.CreateScope()).Returns(_scope);
        A.CallTo(() => _scope.ServiceProvider).Returns(_scopedProvider);

        A.CallTo(() => _scopedProvider.GetService(typeof(NotificationDbContext))).Returns(_dbContext);
        A.CallTo(() => _scopedProvider.GetService(typeof(NotificationProviderFactory))).Returns(_providerFactory);
        A.CallTo(() => _scopedProvider.GetService(typeof(IWebhookService))).Returns(_webhookService);

        // Setup provider resolution for factory
        A.CallTo(() => _scopedProvider.GetService(typeof(MockEmailProvider))).Returns(_notificationProvider);
        A.CallTo(() => _scopedProvider.GetService(typeof(MockSmsProvider))).Returns(_notificationProvider);
    }

    [Fact]
    public async Task ExecuteAsync_WithPendingNotification_ProcessesSuccessfully()
    {
        // Arrange
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            Type = NotificationType.Email,
            Recipient = "test@example.com",
            Status = NotificationStatus.Pending,
            Body = "Test Body"
        };

        await _dbContext.Notifications.AddAsync(notification);
        await _dbContext.SaveChangesAsync();

        // Queue returns the notification once, then null to stop the loop
        A.CallTo(() => _notificationQueue.DequeueAsync(A<CancellationToken>._))
            .ReturnsNextFromSequence(notification, null);

        // Provider returns success
        var providerResult = new NotificationResult(true, "ExternalId", "Sent");
        // No need to mock factory.GetProvider anymore, as we use real factory which uses provider
        A.CallTo(() => _notificationProvider.SendAsync(A<Notification>._, A<CancellationToken>._))
            .Returns(providerResult);
        A.CallTo(() => _notificationProvider.ProviderName).Returns("MockProvider");

        var service = new NotificationProcessorService(_rootProvider, _notificationQueue, _logger);
        using var cts = new CancellationTokenSource();

        // Act
        
        await service.StartAsync(cts.Token);
        await Task.Delay(500); // Give it time to process
        await service.StopAsync(cts.Token);
        

        // Assert
        var dbNotification = await _dbContext.Notifications.FindAsync(notification.Id);
        Assert.NotNull(dbNotification);
        Assert.Equal(NotificationStatus.Sent, dbNotification.Status);
        Assert.Equal("ExternalId", dbNotification.ExternalId);

        A.CallTo(() => _webhookService.SendWebhookAsync(A<Guid>._, A<WebhookEventPayload>._, A<CancellationToken>._))
            .MustHaveHappened();
    }

    [Fact]
    public async Task ExecuteAsync_WithFailedProvider_RetriesNotification()
    {
        // Arrange
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            Type = NotificationType.Email,
            Recipient = "test@example.com",
            Status = NotificationStatus.Pending,
            Body = "Test Body",
            RetryCount = 0,
            MaxRetries = 3
        };

        await _dbContext.Notifications.AddAsync(notification);
        await _dbContext.SaveChangesAsync();

        A.CallTo(() => _notificationQueue.DequeueAsync(A<CancellationToken>._))
            .ReturnsNextFromSequence(notification, null);

        var providerResult = new NotificationResult(false, null, "Error");

        A.CallTo(() => _notificationProvider.SendAsync(A<Notification>._, A<CancellationToken>._))
            .Returns(providerResult);
        A.CallTo(() => _notificationProvider.ProviderName).Returns("MockProvider");

        var service = new NotificationProcessorService(_rootProvider, _notificationQueue, _logger);
        using var cts = new CancellationTokenSource();
        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(500);
        await service.StopAsync(cts.Token);

        // Assert
        var dbNotification = await _dbContext.Notifications.FindAsync(notification.Id);
        Assert.NotNull(dbNotification);
        Assert.Equal(NotificationStatus.Retrying, dbNotification.Status);
        Assert.Equal(1, dbNotification.RetryCount);
    }
}
