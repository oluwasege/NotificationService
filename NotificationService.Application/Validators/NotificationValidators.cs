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
            .When(x => x.Type == NotificationType.Email && x.TemplateId == null);

        RuleFor(x => x.Recipient)
            .Matches(@"^\+?[1-9]\d{1,14}$").WithMessage("Invalid phone number format (E.164)")
            .When(x => x.Type == NotificationType.Sms);

        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Subject is required for emails")
            .MaximumLength(500).WithMessage("Subject must not exceed 500 characters")
            .When(x => x.Type == NotificationType.Email && x.TemplateId == null);

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Body is required")
            .MaximumLength(10000).WithMessage("Body must not exceed 10000 characters")
            .When(x => x.TemplateId == null);

        RuleFor(x => x.Body)
            .MaximumLength(160).WithMessage("SMS body must not exceed 160 characters")
            .When(x => x.Type == NotificationType.Sms && x.TemplateId == null);

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

        RuleFor(x => x.IdempotencyKey)
            .MaximumLength(64).WithMessage("IdempotencyKey must not exceed 64 characters")
            .When(x => !string.IsNullOrEmpty(x.IdempotencyKey));

        RuleFor(x => x.TemplateId)
            .NotEmpty().WithMessage("TemplateId cannot be an empty GUID")
            .When(x => x.TemplateId.HasValue && x.TemplateId == Guid.Empty);
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

public class CreateTemplateRequestValidator : AbstractValidator<CreateTemplateRequest>
{
    public CreateTemplateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Template name is required")
            .MaximumLength(200).WithMessage("Template name must not exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters");

        RuleFor(x => x.SubjectTemplate)
            .NotEmpty().WithMessage("Subject template is required for emails")
            .MaximumLength(500).WithMessage("Subject template must not exceed 500 characters")
            .When(x => x.Type == NotificationType.Email);

        RuleFor(x => x.BodyTemplate)
            .NotEmpty().WithMessage("Body template is required")
            .MaximumLength(10000).WithMessage("Body template must not exceed 10000 characters");
    }
}

public class CreateWebhookRequestValidator : AbstractValidator<CreateWebhookRequest>
{
    public CreateWebhookRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Webhook name is required")
            .MaximumLength(200).WithMessage("Webhook name must not exceed 200 characters");

        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("Webhook URL is required")
            .MaximumLength(2000).WithMessage("URL must not exceed 2000 characters")
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) && 
                        (uri.Scheme == "https" || uri.Scheme == "http"))
            .WithMessage("Invalid URL format. Must be a valid HTTP or HTTPS URL.");

        RuleFor(x => x.Events)
            .NotEmpty().WithMessage("At least one event is required")
            .MaximumLength(500).WithMessage("Events must not exceed 500 characters");
    }
}
