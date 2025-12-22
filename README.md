# Enterprise Notification Service

A production-ready enterprise notification system built with .NET 9, featuring event-driven architecture, guaranteed delivery, and support for Email and SMS notifications.

## Features

### Core Capabilities
- **Multi-Channel Notifications**: Send Email and SMS notifications through a unified API
- **Event-Driven Architecture**: Asynchronous processing with priority-based queuing
- **Guaranteed Delivery**: Automatic retry with exponential backoff (up to 3 retries)
- **Transactional Outbox Pattern**: Ensures reliable message delivery with database consistency

### Enterprise Features
- **Notification Templates**: Reusable templates with Scriban syntax for dynamic variable substitution
- **Webhook Subscriptions**: Real-time delivery status updates via configurable webhooks
- **Scheduled Notifications**: Schedule notifications for future delivery
- **Idempotency Support**: Prevent duplicate notifications with idempotency keys
- **In-Memory Caching**: Performance optimization with configurable cache expiration

### Security & Access Control
- **Subscription-Based Authentication**: Unique API keys for each client subscription
- **JWT Admin Authentication**: Secure admin portal with role-based access control
- **Rate Limiting**: Daily and monthly quotas per subscription

### Monitoring & Management
- **Real-time Dashboard**: Monitor notifications, users, and system health
- **Comprehensive Logging**: Structured logging with Serilog
- **Health Checks**: Built-in health endpoints with dependency monitoring

### Architecture
- **Clean Architecture**: Domain-Driven Design with separation of concerns
- **Code-First Database**: EF Core with SQL Server
- **Unit Tested**: Comprehensive test coverage with xUnit and FakeItEasy

---

## Prerequisites

- **Visual Studio 2022** (Version 17.8 or later recommended)
- **.NET 9 SDK**
- **SQL Server** (LocalDB, SQL Server Express, or full SQL Server)
- **Git** (optional, for cloning)

---

## Setup Instructions

### Step 1: Clone or Download the Solution

Open the solution in Visual Studio 2022:
1. Launch Visual Studio 2022
2. Select **File ? Open ? Project/Solution**
3. Navigate to and select `NotificationService.sln`

### Step 2: Restore NuGet Packages

Visual Studio should automatically restore packages. If not:
1. Right-click on the solution in Solution Explorer
2. Select **Restore NuGet Packages**

Or via terminal:
```bash
dotnet restore NotificationService.sln
```

### Step 3: Configure the Database Connection

Update the connection string in `NotificationService.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=NotificationServiceDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
  }
}
```

For SQL Server Express:
```json
"DefaultConnection": "Server=.\\SQLEXPRESS;Database=NotificationServiceDb;Trusted_Connection=True;TrustServerCertificate=True"
```

For LocalDB:
```json
"DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=NotificationServiceDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
```

### Step 4: Apply Database Migrations

**Option A: Using Visual Studio Package Manager Console**
1. Open **Tools ? NuGet Package Manager ? Package Manager Console**
2. Set **Default project** to `NotificationService.Infrastructure`
3. Run:
```powershell
Update-Database -StartupProject NotificationService.Api
```

**Option B: Using .NET CLI**
```bash
cd NotificationService.Api
dotnet ef database update --project ../NotificationService.Infrastructure
```

> **Note**: The database will also be created automatically when you first run the application.

### Step 5: Run the Application

**Option A: Using Visual Studio**
1. Set `NotificationService.Api` as the startup project
2. Press **F5** or click the green **Start** button
3. The API will launch and open Swagger UI at `https://localhost:<port>/`

**Option B: Using .NET CLI**
```bash
cd NotificationService.Api
dotnet run
```

### Step 6: Access the Application

- **Swagger UI**: `https://localhost:<port>/` (root URL)
- **Health Check**: `https://localhost:<port>/api/health`

---

## Running Tests

The solution includes a comprehensive test suite using xUnit and FakeItEasy.

### Run All Tests

**Using Visual Studio:**
1. Open **Test ? Test Explorer**
2. Click **Run All Tests**

**Using .NET CLI:**
```bash
dotnet test NotificationService.Tests
```

### Test Coverage

The test suite covers:
- **Services**: `AuthServiceTests`, `DashboardServiceTests`, `NotificationAppServiceTests`, `SubscriptionServiceTests`, `SubscriptionValidationServiceTests`, `TemplateServiceTests`, `UserServiceTests`, `WebhookServiceTests`
- **Background Services**: `NotificationProcessorServiceTests`
- **Controllers**: `NotificationsControllerTests`

---

## Seeded Test Data

The application automatically seeds the following test data on first run:

### Admin Users

| Email | Password | Role |
|-------|----------|------|
| admin@notificationservice.com | Admin@123 | SuperAdmin |
| john.smith@acmecorp.com | User@123 | Admin |

### API Subscription Keys

| Key | User | Description |
|-----|------|-------------|
| `sk_live_acme_a1b2c3d4e5f6g7h8i9j0` | Acme Corp | Production key (5000 daily limit) |
| `sk_test_acme_z9y8x7w6v5u4t3s2r1q0` | Acme Corp | Test key (100 daily limit) |
| `sk_live_techstart_m1n2o3p4q5r6s7t8u9v0` | TechStart Inc | Production key |
| `sk_live_global_k1l2m3n4o5p6q7r8s9t0` | Global Services | Email only |

### Notification Templates

The seeded data includes sample templates for:
- Welcome Email
- Password Reset
- Order Confirmation
- OTP Verification
- Weekly Reports
- Invoice Emails

### Webhook Subscriptions

Pre-configured webhooks for testing webhook delivery functionality.

---

## Authentication

### Subscription Key Authentication (For API Clients)

Send the subscription key in the `X-Subscription-Key` header:

```bash
curl -X POST "https://localhost:<port>/api/notifications" \
  -H "X-Subscription-Key: sk_live_acme_a1b2c3d4e5f6g7h8i9j0" \
  -H "Content-Type: application/json" \
  -d '{
    "type": "Email",
    "recipient": "user@example.com",
    "subject": "Hello!",
    "body": "This is a test notification."
  }'
```

### JWT Authentication (For Admin Portal)

1. **Login to get JWT token:**
```bash
curl -X POST "https://localhost:<port>/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@notificationservice.com",
    "password": "Admin@123"
  }'
```

2. **Use the token in subsequent requests:**
```bash
curl -X GET "https://localhost:<port>/api/admin/dashboard" \
  -H "Authorization: Bearer <your-jwt-token>"
```

---

## API Endpoints

### Notification Endpoints

These endpoints require the `X-Subscription-Key` header for authentication.

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/notifications` | Send a single notification (Email or SMS) |
| `POST` | `/api/notifications/batch` | Send multiple notifications in a batch (max 1000) |
| `GET` | `/api/notifications` | List notifications with filtering and pagination |
| `GET` | `/api/notifications/{id}` | Get notification details including delivery logs |
| `POST` | `/api/notifications/{id}/cancel` | Cancel a pending notification |
| `POST` | `/api/notifications/{id}/retry` | Retry a failed notification |
| `GET` | `/api/notifications/quota` | Get remaining daily and monthly quota |

### Authentication Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/auth/login` | Authenticate admin user and receive JWT token |
| `GET` | `/api/auth/me` | Get current authenticated user information |

### Admin Dashboard Endpoints

These endpoints require JWT authentication with Admin or SuperAdmin role.

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/admin/dashboard` | Get comprehensive dashboard summary |
| `GET` | `/api/admin/dashboard/top-users` | Get top users by notification volume |
| `GET` | `/api/admin/dashboard/stats` | Get daily notification statistics for date range |

### Admin User Management Endpoints

These endpoints require JWT authentication with Admin or SuperAdmin role.

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/admin/users` | List all users with pagination |
| `GET` | `/api/admin/users/{id}` | Get user details including subscriptions |
| `POST` | `/api/admin/users` | Create a new user (SuperAdmin only) |
| `PUT` | `/api/admin/users/{id}` | Update user details |
| `DELETE` | `/api/admin/users/{id}` | Soft delete a user (SuperAdmin only) |

### Admin Subscription Management Endpoints

These endpoints require JWT authentication with Admin or SuperAdmin role.

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/admin/subscriptions` | List all subscriptions with optional user filter |
| `GET` | `/api/admin/subscriptions/{id}` | Get subscription details |
| `POST` | `/api/admin/subscriptions` | Create a new subscription |
| `PUT` | `/api/admin/subscriptions/{id}` | Update subscription settings |
| `POST` | `/api/admin/subscriptions/{id}/regenerate-key` | Regenerate subscription API key |
| `DELETE` | `/api/admin/subscriptions/{id}` | Soft delete a subscription |

### Admin Notification Management Endpoints

These endpoints require JWT authentication with Admin or SuperAdmin role.

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/admin/notifications` | List all notifications across all users |
| `GET` | `/api/admin/notifications/{id}` | Get notification details with delivery logs |

### Health Check Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/health` | Basic health check (ASP.NET Health Checks) |
| `GET` | `/health/live` | Liveness probe |
| `GET` | `/health/ready` | Readiness probe (checks database) |
| `GET` | `/api/health/detailed` | Detailed health with database, providers, and queue status |
| `GET` | `/api/health/stats` | System statistics and success rates |

---

## Notification Types

### Email Notification

```json
{
  "type": "Email",
  "recipient": "user@example.com",
  "subject": "Welcome!",
  "body": "<h1>Welcome to our service!</h1><p>Thanks for joining.</p>",
  "priority": "Normal",
  "correlationId": "order-12345"
}
```

### SMS Notification

```json
{
  "type": "Sms",
  "recipient": "+12025551234",
  "subject": "Verification",
  "body": "Your code is: 123456",
  "priority": "High"
}
```

### Scheduled Notification

```json
{
  "type": "Email",
  "recipient": "user@example.com",
  "subject": "Reminder",
  "body": "Don't forget your appointment tomorrow!",
  "scheduledAt": "2024-12-25T10:00:00Z"
}
```

### Template-Based Notification

```json
{
  "type": "Email",
  "recipient": "user@example.com",
  "templateId": "10000000-0000-0000-0000-000000000001",
  "templateData": {
    "user_name": "John Doe",
    "company_name": "Acme Corp",
    "email": "john@example.com",
    "account_id": "ACC-12345"
  }
}
```

### Idempotent Notification

```json
{
  "type": "Email",
  "recipient": "user@example.com",
  "subject": "Order Confirmation",
  "body": "Your order has been confirmed.",
  "idempotencyKey": "order-confirmation-12345"
}
```

### Priority Levels

| Priority | Use Case |
|----------|----------|
| `Low` | Background notifications, newsletters |
| `Normal` | Standard transactional messages (default) |
| `High` | Important alerts, OTPs |
| `Critical` | Security alerts, critical system notifications |

---

## Templates

Templates use [Scriban](https://github.com/scriban/scriban) syntax for dynamic content.

### Creating a Template

```json
{
  "name": "Welcome Email",
  "description": "Welcome email for new users",
  "type": "Email",
  "subjectTemplate": "Welcome to {{ company_name }}, {{ user_name }}!",
  "bodyTemplate": "<h1>Hello {{ user_name }}!</h1><p>Welcome to {{ company_name }}.</p>"
}
```

### Template Syntax Examples

```scriban
{{ variable_name }}                    # Simple variable substitution
{{ if condition }}...{{ end }}         # Conditional rendering
{{ for item in items }}...{{ end }}    # Loop iteration
```

---

## Webhooks

Webhooks provide real-time notifications about delivery status changes.

### Creating a Webhook

```json
{
  "name": "Status Updates",
  "url": "https://your-server.com/webhook",
  "events": "Sent,Delivered,Failed",
  "secret": "your-webhook-secret"
}
```

### Webhook Payload

```json
{
  "notificationId": "guid",
  "status": "Delivered",
  "type": "Email",
  "recipient": "user@example.com",
  "timestamp": "2024-12-22T10:30:00Z",
  "errorMessage": null,
  "externalId": "provider-id"
}
```

### Webhook Signature Verification

Webhooks include an HMAC-SHA256 signature in the `X-Webhook-Signature` header for verification.

---

## Architecture

```
NotificationService.sln
??? NotificationService.Api              # Web API Layer
?   ??? Controllers/                     # API Controllers
?   ??? Middleware/                      # Custom middleware
?   ??? Extensions/                      # Extension methods
?
??? NotificationService.Application      # Application Layer
?   ??? DTOs/                            # Data Transfer Objects
?   ??? Interfaces/                      # Service interfaces
?   ??? Services/                        # Business logic
?   ??? Validators/                      # FluentValidation
?
??? NotificationService.Domain           # Domain Layer
?   ??? Entities/                        # Domain entities
?   ??? Enums/                           # Enumerations
?   ??? Exceptions/                      # Custom exceptions
?   ??? Interfaces/                      # Repository interfaces
?
??? NotificationService.Infrastructure   # Infrastructure Layer
?   ??? BackgroundServices/              # Background processors
?   ??? Caching/                         # Memory cache implementation
?   ??? Data/                            # DbContext & migrations
?   ??? Providers/                       # Mock SMS/Email providers
?   ??? Queue/                           # In-memory notification queue
?   ??? Repositories/                    # Generic repository & UoW
?   ??? Seeding/                         # Database seeder
?
??? NotificationService.Tests            # Test Project
    ??? BackgroundServices/              # Background service tests
    ??? Controllers/                     # Controller tests
    ??? Helpers/                         # Test utilities
    ??? Services/                        # Service tests
```

---

## Event-Driven Processing

The notification system uses an event-driven architecture with multiple background services.

### Notification Flow

1. **Notification Created**: API receives request, validates, and stores in database
2. **Outbox Entry**: Creates outbox message for transactional consistency
3. **Queue Enqueued**: Notification added to priority-based in-memory queue
4. **Background Processing**: `NotificationProcessorService` picks up notifications
5. **Provider Dispatch**: Routes to appropriate provider (Email/SMS)
6. **Webhook Delivery**: Sends status updates to registered webhooks
7. **Retry Logic**: Failed notifications automatically retry with exponential backoff
8. **Delivery Confirmation**: Status updated to "Delivered" (simulated)

### Background Services

| Service | Description |
|---------|-------------|
| `NotificationProcessorService` | Processes notifications from the priority queue |
| `ScheduledNotificationService` | Picks up scheduled notifications when due |
| `OutboxProcessorService` | Ensures reliable delivery via transactional outbox pattern |

### Priority Queue

| Channel | Priority Levels |
|---------|-----------------|
| High Priority | Critical, High |
| Normal Priority | Normal |
| Low Priority | Low |

---

## Monitoring & Logging

### Log Files

Logs are written to the `logs/` directory:
- **Format**: `notification-service-YYYYMMDD.log`
- **Retention**: 30 days

### Log Levels

| Level | Description |
|-------|-------------|
| `Debug` | Detailed diagnostic information |
| `Information` | General operational events |
| `Warning` | Non-critical issues |
| `Error` | Errors that need attention |
| `Fatal` | Critical failures |

### Dashboard Metrics

The admin dashboard provides:
- Total and active users and subscriptions
- Notification statistics (pending, sent, delivered, failed)
- Last 7 days trend
- System health status
- Queue size monitoring

---

## Testing with Swagger

1. Open Swagger UI at the root URL
2. **For notification endpoints:**
   - Click **Authorize**
   - Enter subscription key in the `X-Subscription-Key` field
   - Click **Authorize**
3. **For admin endpoints:**
   - First, call `/api/auth/login` to get a token
   - Click **Authorize**
   - Enter `Bearer <your-token>` in the Authorization field
4. Test any endpoint using the **Try it out** button

---

## Configuration

### JWT Settings

```json
{
  "Jwt": {
    "Secret": "YourSuperSecretKeyThatIsAtLeast32CharactersLongForProduction2024!",
    "Issuer": "NotificationService",
    "Audience": "NotificationServiceClients",
    "ExpiresInMinutes": 60
  }
}
```

### Security Salt

```json
{
  "Security": {
    "PasswordSalt": "NotificationServiceSalt2024"
  }
}
```

---

## Production Considerations

Before deploying to production:

1. **Change JWT Secret**: Use a strong, unique secret (minimum 32 characters)
2. **Update Password Salt**: Use a unique salt value
3. **Configure Real Providers**: Replace mock providers with actual SMS/Email services (e.g., SendGrid, Twilio)
4. **Enable HTTPS**: Ensure SSL/TLS is properly configured
5. **Set Up Monitoring**: Configure Application Insights or similar monitoring solution
6. **Database**: Use a proper SQL Server instance (not LocalDB)
7. **Rate Limiting**: Consider adding request rate limiting
8. **Message Queue**: For high volume, consider replacing in-memory queue with RabbitMQ or Azure Service Bus
9. **Distributed Cache**: Replace in-memory cache with Redis for multi-instance deployments
10. **Webhook Security**: Ensure clients verify webhook signatures

---

## License

This project is provided as-is for educational and demonstration purposes.

---

## Support

For issues or questions, please create an issue in the repository.
