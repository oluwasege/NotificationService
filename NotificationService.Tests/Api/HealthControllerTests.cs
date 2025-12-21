using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NotificationService.Api.Controllers;

namespace NotificationService.Tests.Api;

public class HealthControllerTests
{
    private readonly Mock<ILogger<HealthController>> _loggerMock;
    private readonly HealthController _controller;

    public HealthControllerTests()
    {
        _loggerMock = new Mock<ILogger<HealthController>>();
        _controller = new HealthController(_loggerMock.Object);
    }

    [Fact]
    public void Get_ReturnsOkResultWithHealthStatus()
    {
        // Act
        var result = _controller.Get();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().NotBeNull();
        
        // Verify it's an anonymous object with expected properties
        var value = okResult.Value;
        value.Should().NotBeNull();
        value!.GetType().GetProperty("status")!.GetValue(value).Should().Be("Healthy");
        value.GetType().GetProperty("version")!.GetValue(value).Should().Be("1.0.0");
    }

    [Fact]
    public void GetDetailed_ReturnsOkResultWithDetailedHealthStatus()
    {
        // Act
        var result = _controller.GetDetailed();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().NotBeNull();
        
        // Verify it's an anonymous object with expected properties
        var value = okResult.Value;
        value.Should().NotBeNull();
        value!.GetType().GetProperty("status")!.GetValue(value).Should().Be("Healthy");
        value.GetType().GetProperty("version")!.GetValue(value).Should().Be("1.0.0");
        value.GetType().GetProperty("checks").Should().NotBeNull();
    }
}
