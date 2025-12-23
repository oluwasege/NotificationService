using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NotificationService.Api.Middleware;
using NotificationService.Application;
using NotificationService.Infrastructure;
using NotificationService.Infrastructure.Seeding;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using System.Text.Json;

// Configure Serilog early
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "NotificationService")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/notification-service-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting NotificationService API");

    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog
    builder.Host.UseSerilog();

    // Add services from other layers
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddApplication();

    // Add Memory Cache
    builder.Services.AddMemoryCache();

    // Add Health Checks
    builder.Services.AddHealthChecks()
        .AddSqlServer(
            connectionString: builder.Configuration.GetConnectionString("DefaultConnection")!,
            name: "sqlserver",
            tags: ["database", "sql"])
        .AddCheck<NotificationQueueHealthCheck>("notification-queue", tags: ["queue"]);

    // Add OpenTelemetry
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource
            .AddService(
                serviceName: "NotificationService",
                serviceVersion: "1.0.0"))
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
                options.Filter = httpContext =>
                {
                    // Exclude health check endpoints from tracing
                    var path = httpContext.Request.Path.Value;
                    return path != "/health" && path != "/health/live" && path != "/health/ready";
                };
            })
            .AddHttpClientInstrumentation(options =>
            {
                options.RecordException = true;
            })
            .AddEntityFrameworkCoreInstrumentation(options =>
            {
                options.SetDbStatementForText = true;
            })
            .AddConsoleExporter());

    // Add controllers with JSON options
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        });

    // Configure JWT Authentication
    var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "YourSuperSecretKeyThatIsAtLeast32CharactersLong!";
    var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "NotificationService";
    var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "NotificationServiceClients";

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.Zero
        };
    });

    // Configure Authorization Policies
    builder.Services.AddAuthorizationBuilder()
        .AddPolicy("AdminOnly", policy =>
            policy.RequireRole("Admin", "SuperAdmin"))
        .AddPolicy("SuperAdminOnly", policy =>
            policy.RequireRole("SuperAdmin"));

    // Configure Swagger/OpenAPI
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Notification Service API",
            Version = "v1",
            Description = "Enterprise Notification Service with Email and SMS support. " +
                          "Use subscription keys for API access and JWT for admin operations.",
            Contact = new OpenApiContact
            {
                Name = "Oluwasegun Akinpelu",
                Email = "akinpelu53@gmail.com"
            }
        });

        // Add JWT Authentication to Swagger
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "JWT Authorization header. Enter your token in the text input below.\n\nExample: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
        });

        // Add Subscription Key Authentication to Swagger
        options.AddSecurityDefinition("SubscriptionKey", new OpenApiSecurityScheme
        {
            Name = "X-Subscription-Key",
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Description = "Subscription key for API access. Get your key from the admin portal."
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            },
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "SubscriptionKey"
                    }
                },
                Array.Empty<string>()
            }
        });

        options.EnableAnnotations();
    });

    // Add CORS
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    // Add Response Compression
    builder.Services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
    });

    // Add Rate Limiting with per-subscription partitioning
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        
        // Global fixed window limiter
        options.AddFixedWindowLimiter("fixed", limiterOptions =>
        {
            limiterOptions.PermitLimit = 100;
            limiterOptions.Window = TimeSpan.FromMinutes(1);
            limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            limiterOptions.QueueLimit = 2;
        });

        // Per-subscription sliding window limiter
        options.AddPolicy("per-subscription", context =>
        {
            var subscriptionKey = context.Request.Headers["X-Subscription-Key"].FirstOrDefault();
            
            if (string.IsNullOrEmpty(subscriptionKey))
            {
                // Fall back to IP-based limiting for requests without subscription key
                var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: $"ip:{clientIp}",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 50,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 6,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 2
                    });
            }

            return RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: $"sub:{subscriptionKey}",
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 200,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 6,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 5
                });
        });

        options.OnRejected = async (context, cancellationToken) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.HttpContext.Response.ContentType = "application/json";
            
            var response = new
            {
                error = new
                {
                    code = "RATE_LIMIT_EXCEEDED",
                    message = "Too many requests. Please retry after some time.",
                    retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter) 
                        ? retryAfter.TotalSeconds 
                        : 60
                }
            };

            await context.HttpContext.Response.WriteAsJsonAsync(response, cancellationToken);
        };
    });

    var app = builder.Build();

    // Seed database
    using (var scope = app.Services.CreateScope())
    {
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync();
    }

    // Configure middleware pipeline
    app.UseExceptionHandling();
    app.UseRequestLogging();

    // Configure Swagger (available in all environments for testing)
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Notification Service API v1");
        options.RoutePrefix = string.Empty; // Serve Swagger UI at root
        options.DocumentTitle = "Notification Service API";
        options.DefaultModelsExpandDepth(-1); // Hide models by default
    });

    app.UseHttpsRedirection();
    app.UseCors("AllowAll");

    app.UseRateLimiter();

    app.UseResponseCompression();
    app.UseAuthentication();
    app.UseAuthorization();

    app.UseSubscriptionKeyValidation();

    // Map Health Check endpoints
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var response = new
            {
                status = report.Status.ToString(),
                timestamp = DateTime.UtcNow,
                duration = report.TotalDuration.TotalMilliseconds,
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    duration = e.Value.Duration.TotalMilliseconds,
                    description = e.Value.Description,
                    exception = e.Value.Exception?.Message
                })
            };
            await context.Response.WriteAsJsonAsync(response);
        }
    });

    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false, // No checks, just returns healthy
        ResponseWriter = async (context, _) =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { status = "Healthy", timestamp = DateTime.UtcNow });
        }
    });

    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("database"),
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var response = new
            {
                status = report.Status.ToString(),
                timestamp = DateTime.UtcNow,
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString()
                })
            };
            await context.Response.WriteAsJsonAsync(response);
        }
    });

    app.MapControllers();

    Log.Information("NotificationService API started successfully");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}


