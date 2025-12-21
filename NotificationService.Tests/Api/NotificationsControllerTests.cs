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

namespace NotificationService.Tests.Api;

public class NotificationsControllerTests
{
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<IValidator<SendNotificationRequest>> _validatorMock;
    private readonly Mock<IValidator<SendBatchNotificationRequest>> _batchValidatorMock;
    private readonly Mock<ILogger<NotificationsController>> _loggerMock;
    private readonly NotificationsController _controller;

    public NotificationsControllerTests()
    {
        _notificationServiceMock = new Mock<INotificationService>();
        _validatorMock = new Mock<IValidator<SendNotificationRequest>>();
        _batchValidatorMock = new Mock<IValidator<SendBatchNotificationRequest>>();
        _loggerMock = new Mock<ILogger<NotificationsController>>();

        _controller = new NotificationsController(
            _notificationServiceMock.Object,
            _validatorMock.Object,
            _batchValidatorMock.Object,
            _loggerMock.Object
        );

        // Setup HttpContext
        var httpContext = new DefaultHttpContext();
        httpContext.Items["UserId"] = Guid.NewGuid();
        httpContext.Items["SubscriptionId"] = Guid.NewGuid();
        httpContext.Items["RemainingDailyQuota"] = 100;
        httpContext.Items["RemainingMonthlyQuota"] = 1000;
        httpContext.Items["AllowSms"] = true;
        httpContext.Items["AllowEmail"] = true;
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task SendNotification_ValidRequest_ReturnsCreatedResult()
    {
        // Arrange
        var request = new SendNotificationRequest(
            NotificationType.Email,
            "test@example.com",
            "Test",
            "Test Body"
        );

        var response = new SendNotificationResponse(
            Guid.NewGuid(),
            NotificationStatus.Processing,
            "Notification created successfully",
            DateTime.UtcNow
        );

        _validatorMock
            .Setup(x => x.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _notificationServiceMock
            .Setup(x => x.SendNotificationAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.SendNotification(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<CreatedAtActionResult>();
        var createdResult = result.Result as CreatedAtActionResult;
        createdResult!.Value.Should().Be(response);
        createdResult.ActionName.Should().Be(nameof(_controller.GetNotification));
    }

    [Fact]
    public async Task SendNotification_InvalidRequest_ThrowsValidationException()
    {
        // Arrange
        var request = new SendNotificationRequest(
            NotificationType.Email,
            "invalid-email",
            "Test",
            "Test Body"
        );

        var validationErrors = new List<ValidationFailure>
        {
            new ValidationFailure("Recipient", "Invalid email address")
        };

        _validatorMock
            .Setup(x => x.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(validationErrors));

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(async () =>
            await _controller.SendNotification(request, CancellationToken.None));
    }

    [Fact]
    public async Task SendBatchNotifications_ValidRequest_ReturnsOkResult()
    {
        // Arrange
        var request = new SendBatchNotificationRequest(
            new List<SendNotificationRequest>
            {
                new(NotificationType.Email, "test1@example.com", "Test 1", "Body 1"),
                new(NotificationType.Email, "test2@example.com", "Test 2", "Body 2")
            }
        );

        var response = new SendBatchNotificationResponse(2, 2, 0, new List<BatchNotificationResult>());

        _batchValidatorMock
            .Setup(x => x.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _notificationServiceMock
            .Setup(x => x.SendBatchNotificationsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.SendBatchNotifications(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().Be(response);
    }

    [Fact]
    public async Task GetNotification_ExistingNotification_ReturnsOkResult()
    {
        // Arrange
        var userId = (Guid)_controller.HttpContext.Items["UserId"]!;
        var notificationId = Guid.NewGuid();
        var notification = new NotificationDetailDto(
            notificationId,
            NotificationType.Email,
            NotificationStatus.Delivered,
            NotificationPriority.Normal,
            "test@example.com",
            "Test",
            "Body",
            null,
            0,
            3,
            DateTime.UtcNow,
            null,
            null,
            null,
            null,
            null,
            null,
            userId,
            Guid.NewGuid(),
            new List<NotificationLogDto>()
        );

        _notificationServiceMock
            .Setup(x => x.GetNotificationByIdAsync(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);

        // Act
        var result = await _controller.GetNotification(notificationId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().Be(notification);
    }

    [Fact]
    public async Task GetNotification_NotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var notificationId = Guid.NewGuid();

        _notificationServiceMock
            .Setup(x => x.GetNotificationByIdAsync(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationDetailDto?)null);

        // Act
        var result = await _controller.GetNotification(notificationId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetNotification_DifferentUser_ReturnsNotFoundResult()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var notification = new NotificationDetailDto(
            notificationId,
            NotificationType.Email,
            NotificationStatus.Delivered,
            NotificationPriority.Normal,
            "test@example.com",
            "Test",
            "Body",
            null,
            0,
            3,
            DateTime.UtcNow,
            null,
            null,
            null,
            null,
            null,
            null,
            Guid.NewGuid(), // Different user
            Guid.NewGuid(),
            new List<NotificationLogDto>()
        );

        _notificationServiceMock
            .Setup(x => x.GetNotificationByIdAsync(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);

        // Act
        var result = await _controller.GetNotification(notificationId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task CancelNotification_Success_ReturnsOkResult()
    {
        // Arrange
        var notificationId = Guid.NewGuid();

        _notificationServiceMock
            .Setup(x => x.CancelNotificationAsync(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.CancelNotification(notificationId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CancelNotification_Failed_ReturnsBadRequest()
    {
        // Arrange
        var notificationId = Guid.NewGuid();

        _notificationServiceMock
            .Setup(x => x.CancelNotificationAsync(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.CancelNotification(notificationId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RetryNotification_Success_ReturnsOkResult()
    {
        // Arrange
        var notificationId = Guid.NewGuid();

        _notificationServiceMock
            .Setup(x => x.RetryNotificationAsync(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.RetryNotification(notificationId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task RetryNotification_Failed_ReturnsBadRequest()
    {
        // Arrange
        var notificationId = Guid.NewGuid();

        _notificationServiceMock
            .Setup(x => x.RetryNotificationAsync(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.RetryNotification(notificationId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void GetQuotaStatus_ReturnsOkResultWithQuotaInfo()
    {
        // Act
        var result = _controller.GetQuotaStatus();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().NotBeNull();
    }
}
