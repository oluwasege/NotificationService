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

public class SubscriptionServiceTests
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SubscriptionService> _logger;
    private readonly SubscriptionService _subscriptionService;
    private readonly IRepository<Subscription> _subscriptionRepository;
    private readonly IRepository<User> _userRepository;

    public SubscriptionServiceTests()
    {
        _unitOfWork = A.Fake<IUnitOfWork>();
        _logger = A.Fake<ILogger<SubscriptionService>>();
        
        _subscriptionRepository = A.Fake<IRepository<Subscription>>();
        _userRepository = A.Fake<IRepository<User>>();

        A.CallTo(() => _unitOfWork.GetRepository<Subscription>()).Returns(_subscriptionRepository);
        A.CallTo(() => _unitOfWork.GetRepository<User>()).Returns(_userRepository);

        _subscriptionService = new SubscriptionService(_unitOfWork, _logger);
    }

    [Fact]
    public async Task CreateSubscriptionAsync_WithValidUser_ReturnsSubscription()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreateSubscriptionRequest(
            userId,
            "Test Subscription",
            30,
            100,
            1000,
            true,
            true
        );

        var user = new User { Id = userId };
        A.CallTo(() => _userRepository.GetByIdAsync(userId, A<CancellationToken>._))
            .Returns(Task.FromResult(user)); // FIX: Remove explicit <User?>, just use Task.FromResult(user)

        // Act
        var result = await _subscriptionService.CreateSubscriptionAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(request.Name, result.Name);
        Assert.StartsWith("sk_live_", result.SubscriptionKey);
        Assert.Equal(SubscriptionStatus.Active, result.Status);
        
        A.CallTo(() => _subscriptionRepository.AddAsync(A<Subscription>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task CreateSubscriptionAsync_WithInvalidUser_ThrowsException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreateSubscriptionRequest(
            userId,
            "Test Subscription",
            30,
            100,
            1000,
            true,
            true
        );

        // FIX: Use Task.FromResult<User>(null!) to match Task<User> signature and avoid CS8620
        A.CallTo(() => _userRepository.GetByIdAsync(userId, A<CancellationToken>._))
            .Returns(Task.FromResult<User>(null!));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _subscriptionService.CreateSubscriptionAsync(request));
    }

    [Fact]
    public async Task GetSubscriptionByIdAsync_WithValidId_ReturnsSubscription()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var subscription = new Subscription
        {
            Id = subscriptionId,
            Name = "Test Subscription",
            SubscriptionKey = "sk_live_12345678901234567890",
            Status = SubscriptionStatus.Active
        };

        A.CallTo(() => _subscriptionRepository.GetByIdAsync(subscriptionId, A<CancellationToken>._))
            .Returns(subscription);

        // Act
        var result = await _subscriptionService.GetSubscriptionByIdAsync(subscriptionId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(subscriptionId, result.Id);
        Assert.Contains("*", result.SubscriptionKey); // Should be masked
    }

    [Fact]
    public async Task UpdateSubscriptionAsync_WithValidId_UpdatesAndReturnsSubscription()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var subscription = new Subscription
        {
            Id = subscriptionId,
            Name = "Old Name",
            DailyLimit = 100
        };

        A.CallTo(() => _subscriptionRepository.GetByIdAsync(subscriptionId, A<CancellationToken>._))
            .Returns(subscription);

        var request = new UpdateSubscriptionRequest(
            "New Name",
            SubscriptionStatus.Suspended,
            200,
            null,
            null,
            null,
            null
        );

        // Act
        var result = await _subscriptionService.UpdateSubscriptionAsync(subscriptionId, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("New Name", result.Name);
        Assert.Equal(SubscriptionStatus.Suspended, result.Status);
        Assert.Equal(200, result.DailyLimit);
        
        A.CallTo(() => _subscriptionRepository.UpdateAsync(subscription, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task RegenerateKeyAsync_WithValidId_ReturnsNewKey()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var oldKey = "sk_live_oldkey";
        var subscription = new Subscription
        {
            Id = subscriptionId,
            SubscriptionKey = oldKey
        };

        A.CallTo(() => _subscriptionRepository.GetByIdAsync(subscriptionId, A<CancellationToken>._))
            .Returns(subscription);

        // Act
        var result = await _subscriptionService.RegenerateKeyAsync(subscriptionId);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(oldKey, result.NewSubscriptionKey);
        Assert.StartsWith("sk_live_", result.NewSubscriptionKey);
        
        A.CallTo(() => _subscriptionRepository.UpdateAsync(subscription, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }
}
