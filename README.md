# Enterprise Notification Service

A production-ready enterprise notification system built with .NET 9, featuring event-driven architecture, guaranteed delivery, and support for Email and SMS notifications.

## ?? Features

- **Multi-Channel Notifications**: Send Email and SMS notifications through a unified API
- **Event-Driven Architecture**: Asynchronous processing with priority-based queuing
- **Guaranteed Delivery**: Automatic retry with exponential backoff (up to 3 retries)
- **Subscription-Based Authentication**: Unique API keys for each client subscription
- **JWT Admin Authentication**: Secure admin portal with role-based access control
- **Rate Limiting**: Daily and monthly quotas per subscription
- **Real-time Dashboard**: Monitor notifications, users, and system health
- **Comprehensive Logging**: Structured logging with Serilog
- **Clean Architecture**: Domain-Driven Design with separation of concerns
- **Code-First Database**: EF Core with SQL Server

## ?? Prerequisites

- **Visual Studio 2022** (Version 17.8 or later recommended)
- **.NET 9 SDK**
- **SQL Server** (LocalDB, SQL Server Express, or full SQL Server)
- **Git** (optional, for cloning)

## ??? Setup Instructions

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
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=NotificationServiceDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
  }
}
```

For SQL Server Express:
```json
"DefaultConnection": "Server=.\\SQLEXPRESS;Database=NotificationServiceDb;Trusted_Connection=True;TrustServerCertificate=True"
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

## ?? Seeded Test Data

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

## ?? Authentication

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

## ?? API Endpoints

### Notification Endpoints (Require Subscription Key)

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/notifications` | Send a single notification |
| POST | `/api/notifications/batch` | Send multiple notifications (max 1000) |
| GET | `/api/notifications` | List notifications with filtering |
| GET | `/api/notifications/{id}` | Get notification details with logs |
| POST | `/api/notifications/{id}/cancel` | Cancel a pending notification |
| POST | `/api/notifications/{id}/retry` | Retry a failed notification |
| GET | `/api/notifications/quota` | Get remaining quota |

### Admin Endpoints (Require JWT)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/admin/dashboard` | Get dashboard summary |
| GET | `/api/admin/dashboard/top-users` | Get top users by volume |
| GET | `/api/admin/dashboard/stats` | Get notification statistics |
| GET | `/api/admin/users` | List all users |
| GET | `/api/admin/users/{id}` | Get user details |
| POST | `/api/admin/users` | Create new user |
| PUT | `/api/admin/users/{id}` | Update user |
| DELETE | `/api/admin/users/{id}` | Delete user (soft) |
| GET | `/api/admin/subscriptions` | List all subscriptions |
| POST | `/api/admin/subscriptions` | Create subscription |
| PUT | `/api/admin/subscriptions/{id}` | Update subscription |
| POST | `/api/admin/subscriptions/{id}/regenerate-key` | Generate new API key |
| DELETE | `/api/admin/subscriptions/{id}` | Delete subscription |
| GET | `/api/admin/notifications` | List all notifications |
| GET | `/api/admin/notifications/{id}` | Get notification details |

### Authentication Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/auth/login` | Admin login |
| GET | `/api/auth/me` | Get current user info |

### Health Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/health` | Basic health check |
| GET | `/api/health/detailed` | Detailed health with dependencies |

## ?? Notification Types

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

### Priority Levels

- **Low**: Background notifications, newsletters
- **Normal**: Standard transactional messages (default)
- **High**: Important alerts, OTPs
- **Critical**: Security alerts, critical system notifications

## ??? Architecture

```
NotificationService.sln
??? NotificationService.Api          # Web API Layer
?   ??? Controllers/                 # API Controllers
?   ??? Middleware/                  # Custom middleware
?   ??? Extensions/                  # Extension methods
?
??? NotificationService.Application  # Application Layer
?   ??? DTOs/                        # Data Transfer Objects
?   ??? Interfaces/                  # Service interfaces
?   ??? Services/                    # Business logic
?   ??? Validators/                  # FluentValidation
?
??? NotificationService.Domain       # Domain Layer
?   ??? Entities/                    # Domain entities
?   ??? Enums/                       # Enumerations
?   ??? Interfaces/                  # Repository interfaces
?
??? NotificationService.Infrastructure  # Infrastructure Layer
    ??? BackgroundServices/          # Notification processors
    ??? Data/                        # DbContext & migrations
    ??? Providers/                   # Mock SMS/Email providers
    ??? Queue/                       # In-memory notification queue
    ??? Repositories/                # Generic repository
    ??? Seeding/                     # Database seeder
```

## ?? Event-Driven Processing

The notification system uses an event-driven architecture:

1. **Notification Created**: API receives request, validates, stores in DB
2. **Queue Enqueued**: Notification added to priority-based in-memory queue
3. **Background Processing**: `NotificationProcessorService` picks up notifications
4. **Provider Dispatch**: Routes to appropriate provider (Email/SMS)
5. **Retry Logic**: Failed notifications automatically retry with exponential backoff
6. **Delivery Confirmation**: Status updated to "Delivered" (simulated)

### Priority Queue

- **High Priority Channel**: Critical and High priority notifications
- **Normal Priority Channel**: Standard notifications
- **Low Priority Channel**: Background notifications

## ?? Monitoring & Logging

### Log Files

Logs are written to the `logs/` directory:
- Format: `notification-service-YYYYMMDD.log`
- Retention: 30 days

### Log Levels

- **Debug**: Detailed diagnostic information
- **Information**: General operational events
- **Warning**: Non-critical issues
- **Error**: Errors that need attention
- **Fatal**: Critical failures

### Dashboard Metrics

The admin dashboard provides:
- Total/Active users and subscriptions
- Notification statistics (pending, sent, delivered, failed)
- Last 7 days trend
- System health status
- Queue size monitoring

## ?? Testing with Swagger

1. Open Swagger UI at the root URL
2. For notification endpoints:
   - Click **Authorize**
   - Enter subscription key in the `X-Subscription-Key` field
   - Click **Authorize**
3. For admin endpoints:
   - First, call `/api/auth/login` to get a token
   - Click **Authorize**
   - Enter `Bearer <your-token>` in the Authorization field
4. Test any endpoint using the **Try it out** button

## ?? Testing

The project includes a comprehensive unit test suite with **94 tests** targeting **23%+ code coverage**.

### Running Tests

**Using Visual Studio:**
1. Open **Test Explorer** (Test ? Test Explorer)
2. Click **Run All Tests**
3. View test results and code coverage in the Test Explorer

**Using .NET CLI:**
```bash
# Run all tests
dotnet test

# Run tests with detailed output
dotnet test --verbosity detailed

# Run tests for a specific project
dotnet test NotificationService.Tests/NotificationService.Tests.csproj
```

### Generating Coverage Reports

**Using Coverlet (included in test project):**
```bash
# Run tests and generate coverage report
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Generate HTML coverage report (requires reportgenerator)
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:NotificationService.Tests/coverage/coverage.opencover.xml -targetdir:coveragereport -reporttypes:Html

# View the HTML report
# Open coveragereport/index.html in your browser
```

### Test Structure

The test project covers:
- **Application Layer**: Services, validators, and business logic (39 tests)
- **API Layer**: Controllers and HTTP endpoints (15 tests)
- **Domain Layer**: Entity validation and domain logic (3 tests)
- **Validators**: FluentValidation rules for all DTOs (40 tests)

## ?? JSON Request/Response Examples

### Sending an Email Notification

**Request:**
```http
POST /api/notifications
Content-Type: application/json
X-Subscription-Key: sk_live_acme_a1b2c3d4e5f6g7h8i9j0

{
  "type": "Email",
  "recipient": "user@example.com",
  "subject": "Welcome to Our Service!",
  "body": "<h1>Welcome!</h1><p>Thank you for joining us.</p>",
  "priority": "Normal"
}
```

**Response (201 Created):**
```json
{
  "notificationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Processing",
  "message": "Notification created successfully",
  "createdAt": "2025-12-21T10:30:00Z"
}
```

### Sending an SMS Notification

**Request:**
```http
POST /api/notifications
Content-Type: application/json
X-Subscription-Key: sk_live_acme_a1b2c3d4e5f6g7h8i9j0

{
  "type": "Sms",
  "recipient": "+12025551234",
  "subject": "Verification Code",
  "body": "Your verification code is: 123456",
  "priority": "High"
}
```

**Response (201 Created):**
```json
{
  "notificationId": "7c8f9e45-2341-4892-a7d3-5e6f7a8b9c0d",
  "status": "Processing",
  "message": "Notification created successfully",
  "createdAt": "2025-12-21T10:35:00Z"
}
```

### Sending Batch Notifications

**Request:**
```http
POST /api/notifications/batch
Content-Type: application/json
X-Subscription-Key: sk_live_acme_a1b2c3d4e5f6g7h8i9j0

{
  "notifications": [
    {
      "type": "Email",
      "recipient": "user1@example.com",
      "subject": "Newsletter",
      "body": "Monthly newsletter content",
      "priority": "Low"
    },
    {
      "type": "Email",
      "recipient": "user2@example.com",
      "subject": "Newsletter",
      "body": "Monthly newsletter content",
      "priority": "Low"
    }
  ]
}
```

**Response (200 OK):**
```json
{
  "totalCount": 2,
  "acceptedCount": 2,
  "rejectedCount": 0,
  "results": [
    {
      "index": 0,
      "notificationId": "1a2b3c4d-5e6f-7a8b-9c0d-1e2f3a4b5c6d",
      "accepted": true,
      "errorMessage": null
    },
    {
      "index": 1,
      "notificationId": "2b3c4d5e-6f7a-8b9c-0d1e-2f3a4b5c6d7e",
      "accepted": true,
      "errorMessage": null
    }
  ]
}
```

### Admin Login

**Request:**
```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "admin@notificationservice.com",
  "password": "Admin@123"
}
```

**Response (200 OK):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "tokenType": "Bearer",
  "expiresIn": 3600,
  "user": {
    "id": "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d",
    "name": "System Administrator",
    "email": "admin@notificationservice.com",
    "role": "SuperAdmin",
    "isActive": true,
    "createdAt": "2025-01-01T00:00:00Z",
    "lastLoginAt": "2025-12-21T10:40:00Z"
  }
}
```

### Creating a User

**Request:**
```http
POST /api/admin/users
Content-Type: application/json
Authorization: Bearer <your-jwt-token>

{
  "name": "John Doe",
  "email": "john.doe@example.com",
  "password": "SecurePass123!",
  "role": "Admin"
}
```

**Response (201 Created):**
```json
{
  "id": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
  "name": "John Doe",
  "email": "john.doe@example.com",
  "role": "Admin",
  "isActive": true,
  "createdAt": "2025-12-21T10:45:00Z",
  "lastLoginAt": null
}
```

### Getting Notification Details

**Request:**
```http
GET /api/notifications/3fa85f64-5717-4562-b3fc-2c963f66afa6
X-Subscription-Key: sk_live_acme_a1b2c3d4e5f6g7h8i9j0
```

**Response (200 OK):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "type": "Email",
  "status": "Delivered",
  "priority": "Normal",
  "recipient": "user@example.com",
  "subject": "Welcome to Our Service!",
  "body": "<h1>Welcome!</h1><p>Thank you for joining us.</p>",
  "metadata": null,
  "retryCount": 0,
  "maxRetries": 3,
  "createdAt": "2025-12-21T10:30:00Z",
  "scheduledAt": null,
  "sentAt": "2025-12-21T10:30:05Z",
  "deliveredAt": "2025-12-21T10:30:10Z",
  "errorMessage": null,
  "externalId": "ext-msg-12345",
  "correlationId": "a1b2c3d4e5f6",
  "userId": "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d",
  "subscriptionId": "4c5d6e7f-8a9b-0c1d-2e3f-4a5b6c7d8e9f",
  "logs": [
    {
      "id": "log-1",
      "status": "Processing",
      "message": "Notification created and queued for processing",
      "details": null,
      "createdAt": "2025-12-21T10:30:00Z"
    },
    {
      "id": "log-2",
      "status": "Delivered",
      "message": "Notification delivered successfully",
      "details": "Message ID: ext-msg-12345",
      "createdAt": "2025-12-21T10:30:10Z"
    }
  ]
}
```

## ?? Package Manager Commands

### Common NuGet Operations

```bash
# Restore all packages for the solution
dotnet restore NotificationService.sln

# Add a new package to a project
dotnet add NotificationService.Api/NotificationService.Api.csproj package PackageName

# Update a package
dotnet add NotificationService.Api/NotificationService.Api.csproj package PackageName --version 1.2.3

# Remove a package
dotnet remove NotificationService.Api/NotificationService.Api.csproj package PackageName

# List installed packages
dotnet list NotificationService.Api/NotificationService.Api.csproj package
```

### Entity Framework Commands

```bash
# Add a new migration
dotnet ef migrations add MigrationName --project NotificationService.Infrastructure --startup-project NotificationService.Api

# Update database to latest migration
dotnet ef database update --project NotificationService.Infrastructure --startup-project NotificationService.Api

# Remove last migration (if not applied)
dotnet ef migrations remove --project NotificationService.Infrastructure --startup-project NotificationService.Api

# Generate SQL script from migrations
dotnet ef migrations script --project NotificationService.Infrastructure --startup-project NotificationService.Api --output migration.sql

# Drop database
dotnet ef database drop --project NotificationService.Infrastructure --startup-project NotificationService.Api
```

### Building and Running

```bash
# Build the entire solution
dotnet build NotificationService.sln

# Build in Release mode
dotnet build NotificationService.sln --configuration Release

# Clean build artifacts
dotnet clean NotificationService.sln

# Run the API
cd NotificationService.Api
dotnet run

# Run with specific environment
dotnet run --environment Production

# Publish for deployment
dotnet publish NotificationService.Api/NotificationService.Api.csproj --configuration Release --output ./publish
```

## ?? Configuration

### JWT Settings

```json
{
  "Jwt": {
    "Secret": "YourSuperSecretKeyThatIsAtLeast32CharactersLong!",
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

## ?? Production Considerations

Before deploying to production:

1. **Change JWT Secret**: Use a strong, unique secret
2. **Update Password Salt**: Use a unique salt value
3. **Configure Real Providers**: Replace mock providers with actual SMS/Email services
4. **Enable HTTPS**: Ensure SSL/TLS is properly configured
5. **Set Up Monitoring**: Configure application insights or similar
6. **Database**: Use a proper SQL Server instance (not LocalDB)
7. **Rate Limiting**: Consider adding request rate limiting
8. **Message Queue**: For high volume, consider replacing in-memory queue with RabbitMQ/Azure Service Bus

## ?? License

This project is provided as-is for educational and demonstration purposes.

## ?? Support

For issues or questions, please create an issue in the repository.
