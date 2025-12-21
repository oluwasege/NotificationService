using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NotificationService.Api.Controllers;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Enums;
using System.Security.Claims;

namespace NotificationService.Tests.Api;

public class AdminControllerTests
{
    private readonly Mock<IDashboardService> _dashboardServiceMock;
    private readonly Mock<IUserService> _userServiceMock;
    private readonly Mock<ISubscriptionService> _subscriptionServiceMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<IValidator<CreateUserRequest>> _createUserValidatorMock;
    private readonly Mock<IValidator<UpdateUserRequest>> _updateUserValidatorMock;
    private readonly Mock<IValidator<CreateSubscriptionRequest>> _createSubscriptionValidatorMock;
    private readonly Mock<IValidator<UpdateSubscriptionRequest>> _updateSubscriptionValidatorMock;
    private readonly Mock<ILogger<AdminController>> _loggerMock;
    private readonly AdminController _controller;

    public AdminControllerTests()
    {
        _dashboardServiceMock = new Mock<IDashboardService>();
        _userServiceMock = new Mock<IUserService>();
        _subscriptionServiceMock = new Mock<ISubscriptionService>();
        _notificationServiceMock = new Mock<INotificationService>();
        _createUserValidatorMock = new Mock<IValidator<CreateUserRequest>>();
        _updateUserValidatorMock = new Mock<IValidator<UpdateUserRequest>>();
        _createSubscriptionValidatorMock = new Mock<IValidator<CreateSubscriptionRequest>>();
        _updateSubscriptionValidatorMock = new Mock<IValidator<UpdateSubscriptionRequest>>();
        _loggerMock = new Mock<ILogger<AdminController>>();

        _controller = new AdminController(
            _dashboardServiceMock.Object,
            _userServiceMock.Object,
            _subscriptionServiceMock.Object,
            _notificationServiceMock.Object,
            _createUserValidatorMock.Object,
            _updateUserValidatorMock.Object,
            _createSubscriptionValidatorMock.Object,
            _updateSubscriptionValidatorMock.Object,
            _loggerMock.Object
        );

        // Setup HttpContext with authenticated user
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, "Admin User")
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    [Fact]
    public async Task GetDashboard_ReturnsDashboardSummary()
    {
        // Arrange
        var summary = new DashboardSummaryDto(
            100, 90, 50, 45,
            new NotificationSummaryDto(1000, 100, 50, 600, 200, 50, 100, 80, 5),
            new List<DailyNotificationStatsDto>(),
            new SystemHealthDto(10, 150.5, 95.0, "Healthy")
        );

        _dashboardServiceMock
            .Setup(x => x.GetDashboardSummaryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary);

        // Act
        var result = await _controller.GetDashboard(CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().Be(summary);
    }

    [Fact]
    public async Task GetUsers_ReturnsPagedUserList()
    {
        // Arrange
        var users = new PagedResult<UserDto>(
            new List<UserDto>(),
            0, 1, 20, 0
        );

        _userServiceMock
            .Setup(x => x.GetUsersAsync(1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        // Act
        var result = await _controller.GetUsers(1, 20, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().Be(users);
    }

    [Fact]
    public async Task GetUser_ExistingUser_ReturnsOk()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserDetailDto(
            userId,
            "John Doe",
            "john@example.com",
            UserRole.User,
            true,
            DateTime.UtcNow,
            null,
            new List<SubscriptionDto>()
        );

        _userServiceMock
            .Setup(x => x.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _controller.GetUser(userId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().Be(user);
    }

    [Fact]
    public async Task GetUser_NonExistingUser_ReturnsNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _userServiceMock
            .Setup(x => x.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDetailDto?)null);

        // Act
        var result = await _controller.GetUser(userId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteUser_ExistingUser_ReturnsNoContent()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _userServiceMock
            .Setup(x => x.DeleteUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteUser(userId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteUser_NonExistingUser_ReturnsNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _userServiceMock
            .Setup(x => x.DeleteUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.DeleteUser(userId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }
}
