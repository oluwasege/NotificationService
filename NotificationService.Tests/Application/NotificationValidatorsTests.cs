using FluentAssertions;
using NotificationService.Application.DTOs;
using NotificationService.Application.Validators;
using NotificationService.Domain.Enums;

namespace NotificationService.Tests.Application;

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
    public async Task SendNotificationRequest_ValidEmailRequest_PassesValidation()
    {
        // Arrange
        var request = new SendNotificationRequest(
            NotificationType.Email,
            "test@example.com",
            "Test Subject",
            "Test Body",
            NotificationPriority.Normal
        );

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task SendNotificationRequest_ValidSmsRequest_PassesValidation()
    {
        // Arrange
        var request = new SendNotificationRequest(
            NotificationType.Sms,
            "+12025551234",
            "Test",
            "Test Body",
            NotificationPriority.High
        );

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task SendNotificationRequest_EmptyRecipient_FailsValidation()
    {
        // Arrange
        var request = new SendNotificationRequest(
            NotificationType.Email,
            "",
            "Test",
            "Test Body"
        );

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Recipient");
    }

    [Fact]
    public async Task SendNotificationRequest_InvalidEmail_FailsValidation()
    {
        // Arrange
        var request = new SendNotificationRequest(
            NotificationType.Email,
            "invalid-email",
            "Test",
            "Test Body"
        );

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Recipient" && e.ErrorMessage.Contains("email"));
    }

    [Fact]
    public async Task SendNotificationRequest_InvalidPhoneNumber_FailsValidation()
    {
        // Arrange
        var request = new SendNotificationRequest(
            NotificationType.Sms,
            "not-a-phone-number",
            "Test",
            "Test Body"
        );

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Recipient" && e.ErrorMessage.Contains("phone"));
    }

    [Fact]
    public async Task SendNotificationRequest_EmptyBody_FailsValidation()
    {
        // Arrange
        var request = new SendNotificationRequest(
            NotificationType.Email,
            "test@example.com",
            "Test",
            ""
        );

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Body");
    }

    [Fact]
    public async Task SendNotificationRequest_SmsBodyTooLong_FailsValidation()
    {
        // Arrange
        var request = new SendNotificationRequest(
            NotificationType.Sms,
            "+12025551234",
            "Test",
            new string('a', 161)
        );

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Body" && e.ErrorMessage.Contains("160"));
    }

    [Fact]
    public async Task SendNotificationRequest_EmailWithoutSubject_FailsValidation()
    {
        // Arrange
        var request = new SendNotificationRequest(
            NotificationType.Email,
            "test@example.com",
            "",
            "Test Body"
        );

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Subject");
    }

    [Fact]
    public async Task SendNotificationRequest_SubjectTooLong_FailsValidation()
    {
        // Arrange
        var request = new SendNotificationRequest(
            NotificationType.Email,
            "test@example.com",
            new string('a', 501),
            "Test Body"
        );

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Subject" && e.ErrorMessage.Contains("500"));
    }

    [Fact]
    public async Task SendBatchNotificationRequest_ValidBatch_PassesValidation()
    {
        // Arrange
        var request = new SendBatchNotificationRequest(
            new List<SendNotificationRequest>
            {
                new(NotificationType.Email, "test1@example.com", "Test 1", "Body 1"),
                new(NotificationType.Email, "test2@example.com", "Test 2", "Body 2")
            }
        );

        // Act
        var result = await _batchValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task SendBatchNotificationRequest_EmptyBatch_FailsValidation()
    {
        // Arrange
        var request = new SendBatchNotificationRequest(
            new List<SendNotificationRequest>()
        );

        // Act
        var result = await _batchValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Notifications");
    }

    [Fact]
    public async Task SendBatchNotificationRequest_TooManyNotifications_FailsValidation()
    {
        // Arrange
        var notifications = new List<SendNotificationRequest>();
        for (int i = 0; i < 1001; i++)
        {
            notifications.Add(new SendNotificationRequest(
                NotificationType.Email,
                $"test{i}@example.com",
                "Test",
                "Test Body"
            ));
        }

        var request = new SendBatchNotificationRequest(notifications);

        // Act
        var result = await _batchValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("1000"));
    }

    [Fact]
    public async Task SendBatchNotificationRequest_InvalidItemInBatch_FailsValidation()
    {
        // Arrange
        var request = new SendBatchNotificationRequest(
            new List<SendNotificationRequest>
            {
                new(NotificationType.Email, "test1@example.com", "Test 1", "Body 1"),
                new(NotificationType.Email, "invalid-email", "Test 2", "Body 2")
            }
        );

        // Act
        var result = await _batchValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Recipient"));
    }
}
