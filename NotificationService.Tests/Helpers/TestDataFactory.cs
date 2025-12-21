using NotificationService.Application.DTOs;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;

namespace NotificationService.Tests.Helpers;

/// <summary>
/// Factory class to generate test data for unit tests
/// </summary>
public static class TestDataFactory
{
    public static User CreateTestUser(
        Guid? id = null,
        string name = "Test User",
        string email = "test@example.com",
        UserRole role = UserRole.User,
        bool isActive = true)
    {
        return new User
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            Email = email,
            PasswordHash = "hashed_password",
            Role = role,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static Subscription CreateTestSubscription(
        Guid? id = null,
        Guid? userId = null,
        string name = "Test Subscription",
        string apiKey = "sk_test_12345",
        bool allowSms = true,
        bool allowEmail = true,
        int dailyLimit = 1000,
        int monthlyLimit = 30000,
        SubscriptionStatus status = SubscriptionStatus.Active)
    {
        return new Subscription
        {
            Id = id ?? Guid.NewGuid(),
            UserId = userId ?? Guid.NewGuid(),
            Name = name,
            SubscriptionKey = apiKey,
            AllowSms = allowSms,
            AllowEmail = allowEmail,
            DailyLimit = dailyLimit,
            MonthlyLimit = monthlyLimit,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static Notification CreateTestNotification(
        Guid? id = null,
        Guid? userId = null,
        Guid? subscriptionId = null,
        NotificationType type = NotificationType.Email,
        string recipient = "test@example.com",
        string subject = "Test Subject",
        string body = "Test Body",
        NotificationStatus status = NotificationStatus.Pending,
        NotificationPriority priority = NotificationPriority.Normal)
    {
        return new Notification
        {
            Id = id ?? Guid.NewGuid(),
            UserId = userId ?? Guid.NewGuid(),
            SubscriptionId = subscriptionId ?? Guid.NewGuid(),
            Type = type,
            Recipient = recipient,
            Subject = subject,
            Body = body,
            Status = status,
            Priority = priority,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static SendNotificationRequest CreateSendNotificationRequest(
        NotificationType type = NotificationType.Email,
        string recipient = "test@example.com",
        string subject = "Test Subject",
        string body = "Test Body",
        NotificationPriority priority = NotificationPriority.Normal)
    {
        return new SendNotificationRequest(
            Type: type,
            Recipient: recipient,
            Subject: subject,
            Body: body,
            Priority: priority
        );
    }

    public static CreateUserRequest CreateUserRequest(
        string name = "Test User",
        string email = "test@example.com",
        string password = "Password123!",
        UserRole role = UserRole.User)
    {
        return new CreateUserRequest(
            Name: name,
            Email: email,
            Password: password,
            Role: role
        );
    }

    public static LoginRequest CreateLoginRequest(
        string email = "test@example.com",
        string password = "Password123!")
    {
        return new LoginRequest(
            Email: email,
            Password: password
        );
    }
}
