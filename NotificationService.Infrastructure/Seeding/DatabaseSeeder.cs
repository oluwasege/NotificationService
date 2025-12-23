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
        await SeedNotificationTemplatesAsync();
        await SeedWebhookSubscriptionsAsync();
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
                PasswordHash = HashPassword("Admin@123"),
                Role = UserRole.SuperAdmin,
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = "John Smith",
                Email = "john.smith@acmecorp.com",
                PasswordHash = HashPassword("User@123"),
                Role = UserRole.Admin,
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Name = "Acme Corporation",
                Email = "api@acmecorp.com",
                PasswordHash = HashPassword("Api@123"),
                Role = UserRole.User,
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Name = "TechStart Inc",
                Email = "api@techstart.io",
                PasswordHash = HashPassword("Api@123"),
                Role = UserRole.User,
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                Name = "Global Services Ltd",
                Email = "notifications@globalservices.com",
                PasswordHash = HashPassword("Api@123"),
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

    private async Task SeedNotificationTemplatesAsync()
    {
        var templates = new List<NotificationTemplate>
        {
            // Acme Corporation Email Templates
            new()
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000001"),
                SubscriptionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Name = "Welcome Email",
                Description = "Welcome email sent to new users after registration",
                Type = NotificationType.Email,
                SubjectTemplate = "Welcome to {{ company_name }}, {{ user_name }}!",
                BodyTemplate = @"
<html>
<body>
    <h1>Welcome, {{ user_name }}!</h1>
    <p>Thank you for joining {{ company_name }}. We're excited to have you on board.</p>
    <p>Here are your account details:</p>
    <ul>
        <li><strong>Email:</strong> {{ email }}</li>
        <li><strong>Account ID:</strong> {{ account_id }}</li>
    </ul>
    <p>If you have any questions, please don't hesitate to contact our support team.</p>
    <p>Best regards,<br>The {{ company_name }} Team</p>
</body>
</html>",
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000002"),
                SubscriptionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Name = "Password Reset",
                Description = "Email sent when user requests password reset",
                Type = NotificationType.Email,
                SubjectTemplate = "Reset Your Password - {{ company_name }}",
                BodyTemplate = @"
<html>
<body>
    <h2>Password Reset Request</h2>
    <p>Hi {{ user_name }},</p>
    <p>We received a request to reset your password. Click the button below to create a new password:</p>
    <p><a href='{{ reset_link }}' style='background-color: #4CAF50; color: white; padding: 14px 20px; text-decoration: none; display: inline-block;'>Reset Password</a></p>
    <p>This link will expire in {{ expiry_hours }} hours.</p>
    <p>If you didn't request this, please ignore this email or contact support if you have concerns.</p>
    <p>Best regards,<br>The {{ company_name }} Team</p>
</body>
</html>",
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000003"),
                SubscriptionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Name = "Order Confirmation",
                Description = "Email sent after successful order placement",
                Type = NotificationType.Email,
                SubjectTemplate = "Order Confirmation #{{ order_id }} - {{ company_name }}",
                BodyTemplate = @"
<html>
<body>
    <h2>Thank You for Your Order!</h2>
    <p>Hi {{ customer_name }},</p>
    <p>Your order has been confirmed. Here are your order details:</p>
    <table border='1' cellpadding='10'>
        <tr><th>Order ID</th><td>{{ order_id }}</td></tr>
        <tr><th>Order Date</th><td>{{ order_date }}</td></tr>
        <tr><th>Total Amount</th><td>${{ total_amount }}</td></tr>
        <tr><th>Shipping Address</th><td>{{ shipping_address }}</td></tr>
    </table>
    {{ if items }}
    <h3>Order Items:</h3>
    <ul>
    {{ for item in items }}
        <li>{{ item.name }} x {{ item.quantity }} - ${{ item.price }}</li>
    {{ end }}
    </ul>
    {{ end }}
    <p>You will receive a shipping confirmation once your order is on its way.</p>
    <p>Best regards,<br>The {{ company_name }} Team</p>
</body>
</html>",
                IsActive = true
            },
            // Acme Corporation SMS Templates
            new()
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000004"),
                SubscriptionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Name = "OTP Verification",
                Description = "SMS template for sending OTP codes",
                Type = NotificationType.Sms,
                SubjectTemplate = "OTP Code",
                BodyTemplate = "Your {{ company_name }} verification code is: {{ otp_code }}. Valid for {{ expiry_minutes }} minutes. Do not share this code.",
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000005"),
                SubscriptionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Name = "Order Status Update",
                Description = "SMS template for order status updates",
                Type = NotificationType.Sms,
                SubjectTemplate = "Order Update",
                BodyTemplate = "{{ company_name }}: Your order #{{ order_id }} is now {{ status }}. {{ if tracking_url }}Track: {{ tracking_url }}{{ end }}",
                IsActive = true
            },
            // TechStart Inc Templates
            new()
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000006"),
                SubscriptionId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                Name = "Weekly Report",
                Description = "Weekly activity summary report",
                Type = NotificationType.Email,
                SubjectTemplate = "Your Weekly Report - Week {{ week_number }}",
                BodyTemplate = @"
<html>
<body>
    <h1>Weekly Activity Report</h1>
    <p>Hi {{ user_name }},</p>
    <p>Here's your activity summary for week {{ week_number }}:</p>
    <ul>
        <li>Tasks Completed: {{ tasks_completed }}</li>
        <li>Hours Logged: {{ hours_logged }}</li>
        <li>Projects Updated: {{ projects_updated }}</li>
    </ul>
    <p>Keep up the great work!</p>
</body>
</html>",
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000007"),
                SubscriptionId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                Name = "Alert Notification",
                Description = "SMS alert for critical notifications",
                Type = NotificationType.Sms,
                SubjectTemplate = "Alert",
                BodyTemplate = "ALERT from {{ app_name }}: {{ alert_message }}. Severity: {{ severity }}. Time: {{ timestamp }}",
                IsActive = true
            },
            // Global Services Templates
            new()
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000008"),
                SubscriptionId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                Name = "Invoice Email",
                Description = "Monthly invoice email template",
                Type = NotificationType.Email,
                SubjectTemplate = "Invoice #{{ invoice_number }} - {{ company_name }}",
                BodyTemplate = @"
<html>
<body>
    <h2>Invoice #{{ invoice_number }}</h2>
    <p>Dear {{ client_name }},</p>
    <p>Please find attached your invoice for {{ billing_period }}.</p>
    <table border='1' cellpadding='10'>
        <tr><th>Invoice Number</th><td>{{ invoice_number }}</td></tr>
        <tr><th>Issue Date</th><td>{{ issue_date }}</td></tr>
        <tr><th>Due Date</th><td>{{ due_date }}</td></tr>
        <tr><th>Amount Due</th><td>${{ amount_due }}</td></tr>
    </table>
    <p>Payment can be made via bank transfer to:</p>
    <ul>
        <li>Bank: {{ bank_name }}</li>
        <li>Account: {{ account_number }}</li>
        <li>Reference: {{ invoice_number }}</li>
    </ul>
    <p>Thank you for your business.</p>
</body>
</html>",
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000009"),
                SubscriptionId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                Name = "Payment Reminder",
                Description = "Payment reminder email",
                Type = NotificationType.Email,
                SubjectTemplate = "Payment Reminder - Invoice #{{ invoice_number }}",
                BodyTemplate = @"
<html>
<body>
    <h2>Payment Reminder</h2>
    <p>Dear {{ client_name }},</p>
    <p>This is a friendly reminder that payment for Invoice #{{ invoice_number }} is due on {{ due_date }}.</p>
    <p><strong>Amount Due:</strong> ${{ amount_due }}</p>
    <p>If you have already made the payment, please disregard this message.</p>
    <p>Thank you.</p>
</body>
</html>",
                IsActive = true
            },
            // Inactive template for testing
            new()
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000010"),
                SubscriptionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Name = "Deprecated Welcome",
                Description = "Old welcome template - no longer in use",
                Type = NotificationType.Email,
                SubjectTemplate = "Welcome!",
                BodyTemplate = "<p>Welcome to our service!</p>",
                IsActive = false
            }
        };

        await _context.NotificationTemplates.AddRangeAsync(templates);
        _logger.LogInformation("Seeded {Count} notification templates", templates.Count);
    }

    private async Task SeedWebhookSubscriptionsAsync()
    {
        var webhooks = new List<WebhookSubscription>
        {
            new()
            {
                Id = Guid.Parse("20000000-0000-0000-0000-000000000001"),
                SubscriptionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Name = "Acme Status Webhook",
                Url = "https://webhook.acmecorp.com/notifications",
                Secret = "whsec_acme_secret_key_12345",
                Events = "Sent,Delivered,Failed",
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("20000000-0000-0000-0000-000000000002"),
                SubscriptionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Name = "Acme Analytics Webhook",
                Url = "https://analytics.acmecorp.com/events",
                Secret = "whsec_acme_analytics_67890",
                Events = "*",
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("20000000-0000-0000-0000-000000000003"),
                SubscriptionId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                Name = "TechStart Slack Integration",
                Url = "https://hooks.slack.com/services/techstart/webhook",
                Secret = "whsec_techstart_slack_abc123",
                Events = "Failed",
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("20000000-0000-0000-0000-000000000004"),
                SubscriptionId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                Name = "Global Services Audit Log",
                Url = "https://audit.globalservices.com/webhook",
                Secret = "whsec_global_audit_xyz789",
                Events = "Sent,Delivered,Failed",
                IsActive = true
            },
            // Inactive webhook for testing
            new()
            {
                Id = Guid.Parse("20000000-0000-0000-0000-000000000005"),
                SubscriptionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Name = "Disabled Webhook",
                Url = "https://disabled.example.com/webhook",
                Secret = "whsec_disabled_test",
                Events = "*",
                IsActive = false,
                FailureCount = 10,
                LastFailureAt = DateTime.UtcNow.AddDays(-1)
            }
        };

        await _context.WebhookSubscriptions.AddRangeAsync(webhooks);
        _logger.LogInformation("Seeded {Count} webhook subscriptions", webhooks.Count);
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
                ExternalId = "mock_email_001",
                CorrelationId = "onboarding-flow-001",
                IdempotencyKey = "welcome-user-001"
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
                ExternalId = "mock_sms_001",
                CorrelationId = "verification-flow-001",
                IdempotencyKey = "otp-user-001-12345"
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
                ScheduledAt = DateTime.UtcNow.AddMinutes(30),
                CorrelationId = "weekly-report-001"
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
                ErrorMessage = "Invalid email format",
                CorrelationId = "invoice-reminder-001"
            },
            // Template-based notification
            new()
            {
                UserId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                SubscriptionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                TemplateId = Guid.Parse("10000000-0000-0000-0000-000000000001"),
                Type = NotificationType.Email,
                Status = NotificationStatus.Delivered,
                Priority = NotificationPriority.Normal,
                Recipient = "newuser@example.com",
                Subject = "Welcome to Acme Corp, Jane Doe!",
                Body = "<html><body><h1>Welcome, Jane Doe!</h1>...</body></html>",
                SentAt = DateTime.UtcNow.AddMinutes(-30),
                DeliveredAt = DateTime.UtcNow.AddMinutes(-30).AddSeconds(4),
                ExternalId = "mock_email_002",
                CorrelationId = "onboarding-jane-001",
                IdempotencyKey = "welcome-jane-doe-001"
            },
            // Critical priority notification
            new()
            {
                UserId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                SubscriptionId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                Type = NotificationType.Sms,
                Status = NotificationStatus.Sent,
                Priority = NotificationPriority.Critical,
                Recipient = "+1987654321",
                Subject = "Security Alert",
                Body = "ALERT: Unusual login detected on your account.",
                SentAt = DateTime.UtcNow.AddMinutes(-5),
                ExternalId = "mock_sms_002",
                CorrelationId = "security-alert-001"
            }
        };

        await _context.Notifications.AddRangeAsync(notifications);
        _logger.LogInformation("Seeded {Count} sample notifications", notifications.Count);
    }

    private static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(12));
    }
}
