using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NotificationService.Api.Controllers;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Enums;
using NotificationService.Tests.Helpers;
using System.Security.Claims;

namespace NotificationService.Tests.Api.Controllers;

public class NotificationsControllerTests
{
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<IValidator<SendNotificationRequest>> _validatorMock;
    private readonly Mock<IValidator<SendBatchNotificationRequest>> _batchValidatorMock;
    private readonly Mock<ILogger<NotificationsController>> _loggerMock;
    private readonly NotificationsController _controller;
    private readonly Guid _testUserId;
    private readonly Guid _testSubscriptionId;

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

        _testUserId = Guid.NewGuid();
        _testSubscriptionId = Guid.NewGuid();

        // Setup HttpContext with user claims
        var claims = new[]
        {
            new Claim("userId", _testUserId.ToString()),
            new Claim("subscriptionId", _testSubscriptionId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };

        // Add userId and subscriptionId to HttpContext items
        _controller.HttpContext.Items["UserId"] = _testUserId;
        _controller.HttpContext.Items["SubscriptionId"] = _testSubscriptionId;
    }

    [Fact]
    public async Task SendNotification_WithValidRequest_ReturnsCreatedResult()
    {
        // Arrange
        var request = TestDataFactory.CreateSendNotificationRequest();
        var expectedResponse = new SendNotificationResponse(
            Guid.NewGuid(),
            NotificationStatus.Processing,
            "Notification created successfully",
            DateTime.UtcNow
        );

        _validatorMock
            .Setup(x => x.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());

        _notificationServiceMock
            .Setup(x => x.SendNotificationAsync(_testUserId, _testSubscriptionId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.SendNotification(request, CancellationToken.None);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(201);
        createdResult.Value.Should().BeEquivalentTo(expectedResponse);
        _notificationServiceMock.Verify(x => x.SendNotificationAsync(_testUserId, _testSubscriptionId, request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendNotification_WithInvalidRequest_ThrowsValidationException()
    {
        // Arrange
        var request = TestDataFactory.CreateSendNotificationRequest();
        var validationErrors = new List<FluentValidation.Results.ValidationFailure>
        {
            new FluentValidation.Results.ValidationFailure("Recipient", "Invalid email address")
        };

        _validatorMock
            .Setup(x => x.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult(validationErrors));

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _controller.SendNotification(request, CancellationToken.None));

        _notificationServiceMock.Verify(
            x => x.SendNotificationAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<SendNotificationRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendBatchNotifications_WithValidRequest_ReturnsOkResult()
    {
        // Arrange
        var request = new SendBatchNotificationRequest(new List<SendNotificationRequest>
        {
            TestDataFactory.CreateSendNotificationRequest(),
            TestDataFactory.CreateSendNotificationRequest(recipient: "test2@example.com")
        });

        var expectedResponse = new SendBatchNotificationResponse(
            2,
            2,
            0,
            new List<BatchNotificationResult>
            {
                new BatchNotificationResult(0, Guid.NewGuid(), true, null),
                new BatchNotificationResult(1, Guid.NewGuid(), true, null)
            }
        );

        _batchValidatorMock
            .Setup(x => x.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());

        _notificationServiceMock
            .Setup(x => x.SendBatchNotificationsAsync(_testUserId, _testSubscriptionId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.SendBatchNotifications(request, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(expectedResponse);
    }

    [Fact]
    public async Task SendBatchNotifications_WithInvalidRequest_ThrowsValidationException()
    {
        // Arrange
        var request = new SendBatchNotificationRequest(new List<SendNotificationRequest>());
        var validationErrors = new List<FluentValidation.Results.ValidationFailure>
        {
            new FluentValidation.Results.ValidationFailure("Notifications", "At least one notification is required")
        };

        _batchValidatorMock
            .Setup(x => x.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult(validationErrors));

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _controller.SendBatchNotifications(request, CancellationToken.None));
    }

    [Fact]
    public async Task GetNotification_WithExistingId_ReturnsOkResult()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var expectedNotification = new NotificationDetailDto(
            notificationId,
            NotificationType.Email,
            NotificationStatus.Delivered,
            NotificationPriority.Normal,
            "test@example.com",
            "Test Subject",
            "Test Body",
            null,
            0,
            3,
            DateTime.UtcNow,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow,
            null,
            null,
            null,
            _testUserId,
            _testSubscriptionId,
            new List<NotificationLogDto>()
        );

        _notificationServiceMock
            .Setup(x => x.GetNotificationByIdAsync(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedNotification);

        // Act
        var result = await _controller.GetNotification(notificationId, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(expectedNotification);
    }

    [Fact]
    public async Task GetNotification_WithNonExistingId_ReturnsNotFound()
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
    public async Task CancelNotification_WithExistingId_ReturnsOkObjectResult()
    {
        // Arrange
        var notificationId = Guid.NewGuid();

        _notificationServiceMock
            .Setup(x => x.CancelNotificationAsync(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.CancelNotification(notificationId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task CancelNotification_WithNonExistingId_ReturnsBadRequest()
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
    public async Task RetryNotification_WithExistingId_ReturnsOkObjectResult()
    {
        // Arrange
        var notificationId = Guid.NewGuid();

        _notificationServiceMock
            .Setup(x => x.RetryNotificationAsync(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.RetryNotification(notificationId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task RetryNotification_WithNonExistingId_ReturnsBadRequest()
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
}
