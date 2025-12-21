using FluentAssertions;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Tests.Helpers;

namespace NotificationService.Tests.Domain;

public class NotificationEntityTests
{
    [Fact]
    public void Notification_Creation_SetsDefaultValues()
    {
        // Act
        var notification = new Notification();

        // Assert
        notification.Status.Should().Be(NotificationStatus.Pending);
        notification.Priority.Should().Be(NotificationPriority.Normal);
        notification.MaxRetries.Should().Be(3);
        notification.RetryCount.Should().Be(0);
    }

    [Fact]
    public void Notification_WithTestData_HasCorrectProperties()
    {
        // Arrange & Act
        var notification = TestDataFactory.CreateTestNotification(
            type: NotificationType.Email,
            recipient: "test@example.com",
            subject: "Test",
            body: "Test body"
        );

        // Assert
        notification.Type.Should().Be(NotificationType.Email);
        notification.Recipient.Should().Be("test@example.com");
        notification.Subject.Should().Be("Test");
        notification.Body.Should().Be("Test body");
    }
}

public class UserEntityTests
{
    [Fact]
    public void User_Creation_SetsDefaultValues()
    {
        // Act
        var user = new User();

        // Assert
        user.IsActive.Should().BeTrue();
        user.Role.Should().Be(UserRole.User);
    }

    [Fact]
    public void User_WithTestData_HasCorrectProperties()
    {
        // Arrange & Act
        var user = TestDataFactory.CreateTestUser(
            name: "Test User",
            email: "test@example.com",
            role: UserRole.Admin
        );

        // Assert
        user.Name.Should().Be("Test User");
        user.Email.Should().Be("test@example.com");
        user.Role.Should().Be(UserRole.Admin);
        user.IsActive.Should().BeTrue();
    }
}

public class SubscriptionEntityTests
{
    [Fact]
    public void Subscription_Creation_SetsDefaultValues()
    {
        // Act
        var subscription = new Subscription();

        // Assert
        subscription.Status.Should().Be(SubscriptionStatus.Active);
        subscription.DailyLimit.Should().Be(1000);
        subscription.MonthlyLimit.Should().Be(30000);
        subscription.AllowSms.Should().BeTrue();
        subscription.AllowEmail.Should().BeTrue();
    }

    [Fact]
    public void Subscription_WithTestData_HasCorrectProperties()
    {
        // Arrange & Act
        var subscription = TestDataFactory.CreateTestSubscription(
            name: "Test Subscription",
            dailyLimit: 5000,
            monthlyLimit: 150000
        );

        // Assert
        subscription.Name.Should().Be("Test Subscription");
        subscription.DailyLimit.Should().Be(5000);
        subscription.MonthlyLimit.Should().Be(150000);
        subscription.Status.Should().Be(SubscriptionStatus.Active);
    }
}
