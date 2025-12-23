using FakeItEasy;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NotificationService.Api.Controllers;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Enums;

namespace NotificationService.Tests.Controllers;

public class NotificationsControllerTests
{
    private readonly INotificationService _notificationService;
    private readonly IValidator<SendNotificationRequest> _validator;
    private readonly IValidator<SendBatchNotificationRequest> _batchValidator;
    private readonly ILogger<NotificationsController> _logger;
    private readonly NotificationsController _controller;

    public NotificationsControllerTests()
    {
        _notificationService = A.Fake<INotificationService>();
        _validator = A.Fake<IValidator<SendNotificationRequest>>();
        _batchValidator = A.Fake<IValidator<SendBatchNotificationRequest>>();
        _logger = A.Fake<ILogger<NotificationsController>>();

        _controller = new NotificationsController(
            _notificationService,
            _validator,
            _batchValidator,
            _logger
        );

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    private void SetupContext(Guid userId, Guid subscriptionId)
    {
        _controller.ControllerContext.HttpContext.Items["UserId"] = userId;
        _controller.ControllerContext.HttpContext.Items["SubscriptionId"] = subscriptionId;
    }

    [Fact]
    public async Task SendNotification_WithValidRequest_ReturnsCreated()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        SetupContext(userId, subscriptionId);

        var request = new SendNotificationRequest(
            NotificationType.Email,
            "test@example.com",
            "Subject",
            "Body",
            NotificationPriority.Normal
        );

        var response = new SendNotificationResponse(
            Guid.NewGuid(),
            NotificationStatus.Processing,
            "Queued",
            DateTime.UtcNow
        );

        A.CallTo(() => _validator.ValidateAsync(request, A<CancellationToken>._))
            .Returns(new ValidationResult());

        A.CallTo(() => _notificationService.SendNotificationAsync(userId, subscriptionId, request, A<CancellationToken>._))
            .Returns(response);

        // Act
        var result = await _controller.SendNotification(request, CancellationToken.None);

        // Assert
        var actionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var returnResponse = Assert.IsType<SendNotificationResponse>(actionResult.Value);
        Assert.Equal(response.NotificationId, returnResponse.NotificationId);
    }

    [Fact]
    public async Task SendNotification_WithInvalidRequest_ThrowsValidationException()
    {
        // Arrange
        var request = new SendNotificationRequest(
            NotificationType.Email,
            "", // Invalid
            "Subject",
            "Body",
            NotificationPriority.Normal
        );

        var validationResult = new ValidationResult(new[] { new ValidationFailure("Recipient", "Required") });

        A.CallTo(() => _validator.ValidateAsync(request, A<CancellationToken>._))
            .Returns(validationResult);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => 
            _controller.SendNotification(request, CancellationToken.None));
    }

    [Fact]
    public async Task GetNotification_WithValidIdAndOwner_ReturnsNotification()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        SetupContext(userId, subscriptionId);

        var notificationId = Guid.NewGuid();
        var notification = new NotificationDetailDto(
            notificationId,
            NotificationType.Email,
            NotificationStatus.Sent,
            NotificationPriority.Normal,
            "test@example.com",
            "Subject",
            "Body",
            null,
            0,
            3,
            DateTime.UtcNow,
            null,
            DateTime.UtcNow,
            null,
            null,
            null,
            null,
            userId, // Matches context user
            subscriptionId,
            new List<NotificationLogDto>()
        );

        A.CallTo(() => _notificationService.GetNotificationByIdAsync(notificationId, A<CancellationToken>._))
            .Returns(notification);

        // Act
        var result = await _controller.GetNotification(notificationId, CancellationToken.None);

        // Assert
        var actionResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnDto = Assert.IsType<NotificationDetailDto>(actionResult.Value);
        Assert.Equal(notificationId, returnDto.Id);
    }

    [Fact]
    public async Task GetNotification_WithDifferentUser_ReturnsNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        SetupContext(userId, Guid.NewGuid());

        var notificationId = Guid.NewGuid();
        var notification = new NotificationDetailDto(
            notificationId,
            NotificationType.Email,
            NotificationStatus.Sent,
            NotificationPriority.Normal,
            "test@example.com",
            "Subject",
            "Body",
            null,
            0,
            3,
            DateTime.UtcNow,
            null,
            DateTime.UtcNow,
            null,
            null,
            null,
            null,
            otherUserId, // Different user
            Guid.NewGuid(),
            new List<NotificationLogDto>()
        );

        A.CallTo(() => _notificationService.GetNotificationByIdAsync(notificationId, A<CancellationToken>._))
            .Returns(notification);

        // Act
        var result = await _controller.GetNotification(notificationId, CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task CancelNotification_WithSuccess_ReturnsOk()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        A.CallTo(() => _notificationService.CancelNotificationAsync(notificationId, A<CancellationToken>._))
            .Returns(true);

        // Act
        var result = await _controller.CancelNotification(notificationId, CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task CancelNotification_WithFailure_ReturnsBadRequest()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        A.CallTo(() => _notificationService.CancelNotificationAsync(notificationId, A<CancellationToken>._))
            .Returns(false);

        // Act
        var result = await _controller.CancelNotification(notificationId, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }
}
