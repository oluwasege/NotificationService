using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Infrastructure.Data;

namespace NotificationService.Infrastructure.Seeding;

public class DatabaseSeeder
{
    private readonly NotificationDbContext _context;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(NotificationDbContext context, ILogger<DatabaseSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        _logger.LogInformation("Starting database seeding...");

        await _context.Database.EnsureCreatedAsync();

        if (await _context.Users.AnyAsync())
        {
            _logger.LogInformation("Database already seeded. Skipping.");
            return;
        }

        await SeedUsersAsync();
        await SeedSubscriptionsAsync();
        await SeedSampleNotificationsAsync();

        await _context.SaveChangesAsync();
        _logger.LogInformation("Database seeding completed successfully.");
    }

    private async Task SeedUsersAsync()
    {
        var users = new List<User>
        {
            new()
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "System Administrator",
                Email = "admin@notificationservice.com",
                PasswordHash = BCryptHash("Admin@123"),
                Role = UserRole.SuperAdmin,
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = "John Smith",
                Email = "john.smith@acmecorp.com",
                PasswordHash = BCryptHash("User@123"),
                Role = UserRole.Admin,
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Name = "Acme Corporation",
                Email = "api@acmecorp.com",
                PasswordHash = BCryptHash("Api@123"),
                Role = UserRole.User,
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Name = "TechStart Inc",
                Email = "api@techstart.io",
                PasswordHash = BCryptHash("Api@123"),
                Role = UserRole.User,
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                Name = "Global Services Ltd",
                Email = "notifications@globalservices.com",
                PasswordHash = BCryptHash("Api@123"),
                Role = UserRole.User,
                IsActive = true
            }
        };

        await _context.Users.AddRangeAsync(users);
        _logger.LogInformation("Seeded {Count} users", users.Count);
    }

    private async Task SeedSubscriptionsAsync()
    {
        var subscriptions = new List<Subscription>
        {
            new()
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                UserId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                SubscriptionKey = "sk_live_acme_a1b2c3d4e5f6g7h8i9j0",
                Name = "Acme Production",
                Status = SubscriptionStatus.Active,
                ExpiresAt = DateTime.UtcNow.AddYears(1),
                DailyLimit = 5000,
                MonthlyLimit = 100000,
                AllowSms = true,
                AllowEmail = true
            },
            new()
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                UserId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                SubscriptionKey = "sk_test_acme_z9y8x7w6v5u4t3s2r1q0",
                Name = "Acme Testing",
                Status = SubscriptionStatus.Active,
                ExpiresAt = DateTime.UtcNow.AddMonths(6),
                DailyLimit = 100,
                MonthlyLimit = 1000,
                AllowSms = true,
                AllowEmail = true
            },
            new()
            {
                Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                UserId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                SubscriptionKey = "sk_live_techstart_m1n2o3p4q5r6s7t8u9v0",
                Name = "TechStart Production",
                Status = SubscriptionStatus.Active,
                ExpiresAt = DateTime.UtcNow.AddYears(1),
                DailyLimit = 2000,
                MonthlyLimit = 50000,
                AllowSms = true,
                AllowEmail = true
            },
            new()
            {
                Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                UserId = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                SubscriptionKey = "sk_live_global_k1l2m3n4o5p6q7r8s9t0",
                Name = "Global Services Production",
                Status = SubscriptionStatus.Active,
                ExpiresAt = DateTime.UtcNow.AddYears(2),
                DailyLimit = 10000,
                MonthlyLimit = 250000,
                AllowSms = false,
                AllowEmail = true
            },
            new()
            {
                Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                UserId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                SubscriptionKey = "sk_suspended_techstart_expired123",
                Name = "TechStart Suspended",
                Status = SubscriptionStatus.Suspended,
                ExpiresAt = DateTime.UtcNow.AddDays(-30),
                DailyLimit = 1000,
                MonthlyLimit = 20000,
                AllowSms = true,
                AllowEmail = true
            }
        };

        await _context.Subscriptions.AddRangeAsync(subscriptions);
        _logger.LogInformation("Seeded {Count} subscriptions", subscriptions.Count);
    }

    private async Task SeedSampleNotificationsAsync()
    {
        var notifications = new List<Notification>
        {
            new()
            {
                UserId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                SubscriptionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Type = NotificationType.Email,
                Status = NotificationStatus.Delivered,
                Priority = NotificationPriority.Normal,
                Recipient = "customer1@example.com",
                Subject = "Welcome to Acme Corp!",
                Body = "Thank you for joining Acme Corp. We're excited to have you!",
                SentAt = DateTime.UtcNow.AddHours(-2),
                DeliveredAt = DateTime.UtcNow.AddHours(-2).AddSeconds(5),
                ExternalId = "mock_email_001"
            },
            new()
            {
                UserId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                SubscriptionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Type = NotificationType.Sms,
                Status = NotificationStatus.Delivered,
                Priority = NotificationPriority.High,
                Recipient = "+1234567890",
                Subject = "Verification Code",
                Body = "Your verification code is: 123456",
                SentAt = DateTime.UtcNow.AddHours(-1),
                DeliveredAt = DateTime.UtcNow.AddHours(-1).AddSeconds(3),
                ExternalId = "mock_sms_001"
            },
            new()
            {
                UserId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                SubscriptionId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                Type = NotificationType.Email,
                Status = NotificationStatus.Pending,
                Priority = NotificationPriority.Normal,
                Recipient = "user@techstart.io",
                Subject = "Your weekly report",
                Body = "Here is your weekly activity summary...",
                ScheduledAt = DateTime.UtcNow.AddMinutes(30)
            },
            new()
            {
                UserId = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                SubscriptionId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                Type = NotificationType.Email,
                Status = NotificationStatus.Failed,
                Priority = NotificationPriority.Normal,
                Recipient = "invalid-email",
                Subject = "Failed notification",
                Body = "This notification failed due to invalid recipient",
                RetryCount = 3,
                ErrorMessage = "Invalid email format"
            }
        };

        await _context.Notifications.AddRangeAsync(notifications);
        _logger.LogInformation("Seeded {Count} sample notifications", notifications.Count);
    }

    private static string BCryptHash(string password)
    {
        // Simple hash for demo - in production use proper BCrypt
        var bytes = System.Text.Encoding.UTF8.GetBytes(password + "NotificationServiceSalt2024");
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
