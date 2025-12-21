using FluentAssertions;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;

namespace NotificationService.Tests.Domain;

public class NotificationEntityTests
{
    [Fact]
    public void Notification_DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var notification = new Notification();

        // Assert
        notification.Status.Should().Be(NotificationStatus.Pending);
        notification.Priority.Should().Be(NotificationPriority.Normal);
        notification.RetryCount.Should().Be(0);
        notification.MaxRetries.Should().Be(3);
        notification.Recipient.Should().Be(string.Empty);
        notification.Subject.Should().Be(string.Empty);
        notification.Body.Should().Be(string.Empty);
        notification.Logs.Should().NotBeNull();
        notification.Logs.Should().BeEmpty();
    }

    [Fact]
    public void Notification_CanSetProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var correlationId = Guid.NewGuid().ToString();

        // Act
        var notification = new Notification
        {
            UserId = userId,
            SubscriptionId = subscriptionId,
            Type = NotificationType.Email,
            Status = NotificationStatus.Processing,
            Priority = NotificationPriority.High,
            Recipient = "test@example.com",
            Subject = "Test Subject",
            Body = "Test Body",
            Metadata = "{\"key\":\"value\"}",
            CorrelationId = correlationId,
            RetryCount = 1
        };

        // Assert
        notification.UserId.Should().Be(userId);
        notification.SubscriptionId.Should().Be(subscriptionId);
        notification.Type.Should().Be(NotificationType.Email);
        notification.Status.Should().Be(NotificationStatus.Processing);
        notification.Priority.Should().Be(NotificationPriority.High);
        notification.Recipient.Should().Be("test@example.com");
        notification.Subject.Should().Be("Test Subject");
        notification.Body.Should().Be("Test Body");
        notification.Metadata.Should().Be("{\"key\":\"value\"}");
        notification.CorrelationId.Should().Be(correlationId);
        notification.RetryCount.Should().Be(1);
    }
}

public class UserEntityTests
{
    [Fact]
    public void User_DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var user = new User();

        // Assert
        user.Name.Should().Be(string.Empty);
        user.Email.Should().Be(string.Empty);
        user.PasswordHash.Should().Be(string.Empty);
        user.IsActive.Should().BeTrue();
        user.Role.Should().Be(UserRole.User);
        user.Subscriptions.Should().NotBeNull();
        user.Subscriptions.Should().BeEmpty();
    }

    [Fact]
    public void User_CanSetProperties()
    {
        // Arrange & Act
        var user = new User
        {
            Name = "John Doe",
            Email = "john@example.com",
            PasswordHash = "hashed_password",
            Role = UserRole.Admin,
            IsActive = true,
            LastLoginAt = DateTime.UtcNow
        };

        // Assert
        user.Name.Should().Be("John Doe");
        user.Email.Should().Be("john@example.com");
        user.PasswordHash.Should().Be("hashed_password");
        user.Role.Should().Be(UserRole.Admin);
        user.IsActive.Should().BeTrue();
        user.LastLoginAt.Should().NotBeNull();
    }
}

public class SubscriptionEntityTests
{
    [Fact]
    public void Subscription_DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var subscription = new Subscription();

        // Assert
        subscription.Name.Should().Be(string.Empty);
        subscription.SubscriptionKey.Should().Be(string.Empty);
        subscription.Status.Should().Be(SubscriptionStatus.Active);
        subscription.DailyLimit.Should().Be(1000);
        subscription.MonthlyLimit.Should().Be(30000);
        subscription.DailyUsed.Should().Be(0);
        subscription.MonthlyUsed.Should().Be(0);
        subscription.AllowSms.Should().BeTrue();
        subscription.AllowEmail.Should().BeTrue();
    }

    [Fact]
    public void Subscription_CanSetProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddYears(1);

        // Act
        var subscription = new Subscription
        {
            UserId = userId,
            Name = "Test Subscription",
            SubscriptionKey = "sk_test_123",
            Status = SubscriptionStatus.Active,
            ExpiresAt = expiresAt,
            DailyLimit = 5000,
            MonthlyLimit = 100000,
            DailyUsed = 100,
            MonthlyUsed = 1000,
            AllowSms = true,
            AllowEmail = false
        };

        // Assert
        subscription.UserId.Should().Be(userId);
        subscription.Name.Should().Be("Test Subscription");
        subscription.SubscriptionKey.Should().Be("sk_test_123");
        subscription.Status.Should().Be(SubscriptionStatus.Active);
        subscription.ExpiresAt.Should().Be(expiresAt);
        subscription.DailyLimit.Should().Be(5000);
        subscription.MonthlyLimit.Should().Be(100000);
        subscription.DailyUsed.Should().Be(100);
        subscription.MonthlyUsed.Should().Be(1000);
        subscription.AllowSms.Should().BeTrue();
        subscription.AllowEmail.Should().BeFalse();
    }
}
