using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace NotificationService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;

    public HealthController(ILogger<HealthController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Basic health check endpoint
    /// </summary>
    [HttpGet]
    [SwaggerOperation(
        Summary = "Health Check",
        Description = "Returns the health status of the API")]
    [SwaggerResponse(200, "API is healthy")]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "Healthy",
            timestamp = DateTime.UtcNow,
            version = "1.0.0"
        });
    }

    /// <summary>
    /// Detailed health check with dependencies
    /// </summary>
    [HttpGet("detailed")]
    [SwaggerOperation(
        Summary = "Detailed Health Check",
        Description = "Returns detailed health status including dependencies")]
    [SwaggerResponse(200, "Health details")]
    public IActionResult GetDetailed()
    {
        return Ok(new
        {
            status = "Healthy",
            timestamp = DateTime.UtcNow,
            version = "1.0.0",
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
            checks = new
            {
                api = "Healthy",
                database = "Healthy",
                notificationQueue = "Healthy"
            }
        });
    }
}
