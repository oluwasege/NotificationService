using System.Net;
using System.Text.Json;
using FluentValidation;

namespace NotificationService.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception, "An unhandled exception occurred: {Message}", exception.Message);

        var (statusCode, errorCode, message, details) = exception switch
        {
            ValidationException validationEx => (
                HttpStatusCode.BadRequest,
                "VALIDATION_ERROR",
                "One or more validation errors occurred",
                validationEx.Errors.Select(e => new { field = e.PropertyName, error = e.ErrorMessage }).ToArray()
            ),
            InvalidOperationException invalidOpEx => (
                HttpStatusCode.BadRequest,
                "INVALID_OPERATION",
                invalidOpEx.Message,
                (object?)null
            ),
            UnauthorizedAccessException => (
                HttpStatusCode.Unauthorized,
                "UNAUTHORIZED",
                "You are not authorized to perform this action",
                (object?)null
            ),
            KeyNotFoundException => (
                HttpStatusCode.NotFound,
                "NOT_FOUND",
                "The requested resource was not found",
                (object?)null
            ),
            ArgumentException argEx => (
                HttpStatusCode.BadRequest,
                "INVALID_ARGUMENT",
                argEx.Message,
                (object?)null
            ),
            _ => (
                HttpStatusCode.InternalServerError,
                "INTERNAL_ERROR",
                "An unexpected error occurred. Please try again later.",
                (object?)null
            )
        };

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var response = new
        {
            error = new
            {
                code = errorCode,
                message,
                details,
                traceId = context.TraceIdentifier
            }
        };

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, jsonOptions));
    }
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}
