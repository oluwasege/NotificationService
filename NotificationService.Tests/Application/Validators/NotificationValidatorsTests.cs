using FluentAssertions;
using NotificationService.Application.DTOs;
using NotificationService.Application.Validators;
using NotificationService.Domain.Enums;
using NotificationService.Tests.Helpers;

namespace NotificationService.Tests.Application.Validators;

public class NotificationValidatorsTests
{
    private readonly SendNotificationRequestValidator _validator;
    private readonly SendBatchNotificationRequestValidator _batchValidator;

    public NotificationValidatorsTests()
    {
        _validator = new SendNotificationRequestValidator();
        _batchValidator = new SendBatchNotificationRequestValidator();
    }

    [Fact]
    public async Task SendNotificationRequestValidator_WithValidEmailRequest_Passes()
    {
        // Arrange
        var request = TestDataFactory.CreateSendNotificationRequest();

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task SendNotificationRequestValidator_WithEmptyRecipient_Fails()
    {
        // Arrange
        var request = new SendNotificationRequest(
            NotificationType.Email,
            "",
            "Subject",
            "Body"
        );

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Recipient");
    }

    [Fact]
    public async Task SendNotificationRequestValidator_WithInvalidEmailFormat_Fails()
    {
        // Arrange
        var request = new SendNotificationRequest(
            NotificationType.Email,
            "invalid-email",
            "Subject",
            "Body"
        );

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Recipient" && e.ErrorMessage.Contains("email"));
    }

    [Fact]
    public async Task SendNotificationRequestValidator_WithValidSms_Passes()
    {
        // Arrange
        var request = new SendNotificationRequest(
            NotificationType.Sms,
            "+12025551234",
            "Subject",
            "Test message"
        );

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task SendNotificationRequestValidator_WithInvalidPhoneNumber_Fails()
    {
        // Arrange
        var request = new SendNotificationRequest(
            NotificationType.Sms,
            "invalid-phone",
            "Subject",
            "Body"
        );

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Recipient" && e.ErrorMessage.Contains("phone"));
    }

    [Fact]
    public async Task SendNotificationRequestValidator_WithEmptySubjectForEmail_Fails()
    {
        // Arrange
        var request = new SendNotificationRequest(
            NotificationType.Email,
            "test@example.com",
            "",
            "Body"
        );

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Subject");
    }

    [Fact]
    public async Task SendNotificationRequestValidator_WithEmptyBody_Fails()
    {
        // Arrange
        var request = new SendNotificationRequest(
            NotificationType.Email,
            "test@example.com",
            "Subject",
            ""
        );

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Body");
    }

    [Fact]
    public async Task SendNotificationRequestValidator_WithLongSmsBody_Fails()
    {
        // Arrange
        var longBody = new string('a', 161);
        var request = new SendNotificationRequest(
            NotificationType.Sms,
            "+12025551234",
            "Subject",
            longBody
        );

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Body" && e.ErrorMessage.Contains("160"));
    }

    [Fact]
    public async Task SendNotificationRequestValidator_WithLongEmailBody_Fails()
    {
        // Arrange
        var longBody = new string('a', 10001);
        var request = new SendNotificationRequest(
            NotificationType.Email,
            "test@example.com",
            "Subject",
            longBody
        );

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Body" && e.ErrorMessage.Contains("10000"));
    }

    [Fact]
    public async Task SendNotificationRequestValidator_WithPastScheduledTime_Fails()
    {
        // Arrange
        var request = new SendNotificationRequest(
            NotificationType.Email,
            "test@example.com",
            "Subject",
            "Body",
            NotificationPriority.Normal,
            DateTime.UtcNow.AddHours(-1)
        );

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ScheduledAt");
    }

    [Fact]
    public async Task SendNotificationRequestValidator_WithFutureScheduledTime_Passes()
    {
        // Arrange
        var request = new SendNotificationRequest(
            NotificationType.Email,
            "test@example.com",
            "Subject",
            "Body",
            NotificationPriority.Normal,
            DateTime.UtcNow.AddHours(1)
        );

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task SendBatchNotificationRequestValidator_WithValidRequests_Passes()
    {
        // Arrange
        var request = new SendBatchNotificationRequest(new List<SendNotificationRequest>
        {
            TestDataFactory.CreateSendNotificationRequest(),
            TestDataFactory.CreateSendNotificationRequest(recipient: "test2@example.com")
        });

        // Act
        var result = await _batchValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task SendBatchNotificationRequestValidator_WithEmptyList_Fails()
    {
        // Arrange
        var request = new SendBatchNotificationRequest(new List<SendNotificationRequest>());

        // Act
        var result = await _batchValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Notifications");
    }

    [Fact]
    public async Task SendBatchNotificationRequestValidator_WithTooManyNotifications_Fails()
    {
        // Arrange
        var notifications = Enumerable.Range(0, 1001)
            .Select(_ => TestDataFactory.CreateSendNotificationRequest())
            .ToList();
        var request = new SendBatchNotificationRequest(notifications);

        // Act
        var result = await _batchValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("1000"));
    }

    [Fact]
    public async Task SendBatchNotificationRequestValidator_WithInvalidNotification_Fails()
    {
        // Arrange
        var request = new SendBatchNotificationRequest(new List<SendNotificationRequest>
        {
            TestDataFactory.CreateSendNotificationRequest(),
            new SendNotificationRequest(NotificationType.Email, "invalid-email", "Subject", "Body")
        });

        // Act
        var result = await _batchValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Notifications"));
    }
}
