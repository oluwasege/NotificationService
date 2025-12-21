using System.Net;
using System.Text.Json;
using NotificationService.Application.Interfaces;

namespace NotificationService.Api.Middleware;

public class SubscriptionKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SubscriptionKeyMiddleware> _logger;
    private const string SubscriptionKeyHeader = "X-Subscription-Key";

    public SubscriptionKeyMiddleware(RequestDelegate next, ILogger<SubscriptionKeyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var requiresSubscriptionKey = endpoint?.Metadata.GetMetadata<RequireSubscriptionKeyAttribute>() != null;

        if (!requiresSubscriptionKey)
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(SubscriptionKeyHeader, out var subscriptionKey) ||
            string.IsNullOrWhiteSpace(subscriptionKey))
        {
            _logger.LogWarning("Missing subscription key from {IP}", context.Connection.RemoteIpAddress);
            await WriteErrorResponse(context, HttpStatusCode.Unauthorized, "Missing subscription key");
            return;
        }

        var validationService = context.RequestServices.GetRequiredService<ISubscriptionValidationService>();
        var result = await validationService.ValidateSubscriptionKeyAsync(subscriptionKey!);

        if (!result.IsValid)
        {
            _logger.LogWarning("Invalid subscription key from {IP}: {Error}",
                context.Connection.RemoteIpAddress, result.ErrorMessage);
            await WriteErrorResponse(context, HttpStatusCode.Unauthorized, result.ErrorMessage ?? "Invalid subscription key");
            return;
        }

        // Store validated subscription info in HttpContext for use in controllers
        context.Items["UserId"] = result.UserId;
        context.Items["SubscriptionId"] = result.SubscriptionId;
        context.Items["AllowSms"] = result.AllowSms;
        context.Items["AllowEmail"] = result.AllowEmail;
        context.Items["RemainingDailyQuota"] = result.RemainingDailyQuota;
        context.Items["RemainingMonthlyQuota"] = result.RemainingMonthlyQuota;

        _logger.LogDebug("Subscription key validated for user {UserId}, subscription {SubscriptionId}",
            result.UserId, result.SubscriptionId);

        await _next(context);
    }

    private static async Task WriteErrorResponse(HttpContext context, HttpStatusCode statusCode, string message)
    {
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var response = new
        {
            error = new
            {
                code = statusCode.ToString(),
                message
            }
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireSubscriptionKeyAttribute : Attribute
{
}

public static class SubscriptionKeyMiddlewareExtensions
{
    public static IApplicationBuilder UseSubscriptionKeyValidation(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SubscriptionKeyMiddleware>();
    }
}
