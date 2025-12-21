using FluentValidation;
using NotificationService.Application.DTOs;
using NotificationService.Domain.Enums;

namespace NotificationService.Application.Validators;

public class SendNotificationRequestValidator : AbstractValidator<SendNotificationRequest>
{
    public SendNotificationRequestValidator()
    {
        RuleFor(x => x.Recipient)
            .NotEmpty().WithMessage("Recipient is required")
            .MaximumLength(256).WithMessage("Recipient must not exceed 256 characters");

        RuleFor(x => x.Recipient)
            .EmailAddress().WithMessage("Invalid email address")
            .When(x => x.Type == NotificationType.Email);

        RuleFor(x => x.Recipient)
            .Matches(@"^\+?[1-9]\d{1,14}$").WithMessage("Invalid phone number format (E.164)")
            .When(x => x.Type == NotificationType.Sms);

        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Subject is required for emails")
            .MaximumLength(500).WithMessage("Subject must not exceed 500 characters")
            .When(x => x.Type == NotificationType.Email);

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Body is required")
            .MaximumLength(10000).WithMessage("Body must not exceed 10000 characters");

        RuleFor(x => x.Body)
            .MaximumLength(160).WithMessage("SMS body must not exceed 160 characters")
            .When(x => x.Type == NotificationType.Sms);

        RuleFor(x => x.ScheduledAt)
            .GreaterThan(DateTime.UtcNow.AddMinutes(-1))
            .WithMessage("Scheduled time must be in the future")
            .When(x => x.ScheduledAt.HasValue);

        RuleFor(x => x.Metadata)
            .MaximumLength(4000).WithMessage("Metadata must not exceed 4000 characters")
            .When(x => !string.IsNullOrEmpty(x.Metadata));

        RuleFor(x => x.CorrelationId)
            .MaximumLength(64).WithMessage("CorrelationId must not exceed 64 characters")
            .When(x => !string.IsNullOrEmpty(x.CorrelationId));
    }
}

public class SendBatchNotificationRequestValidator : AbstractValidator<SendBatchNotificationRequest>
{
    public SendBatchNotificationRequestValidator()
    {
        RuleFor(x => x.Notifications)
            .NotEmpty().WithMessage("At least one notification is required")
            .Must(x => x.Count <= 1000).WithMessage("Maximum 1000 notifications per batch");

        RuleForEach(x => x.Notifications)
            .SetValidator(new SendNotificationRequestValidator());
    }
}
