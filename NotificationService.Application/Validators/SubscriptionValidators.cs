using FluentValidation;
using NotificationService.Application.DTOs;

namespace NotificationService.Application.Validators;

public class CreateSubscriptionRequestValidator : AbstractValidator<CreateSubscriptionRequest>
{
    public CreateSubscriptionRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters");

        RuleFor(x => x.DailyLimit)
            .GreaterThan(0).WithMessage("Daily limit must be greater than 0")
            .LessThanOrEqualTo(100000).WithMessage("Daily limit must not exceed 100,000");

        RuleFor(x => x.MonthlyLimit)
            .GreaterThan(0).WithMessage("Monthly limit must be greater than 0")
            .LessThanOrEqualTo(10000000).WithMessage("Monthly limit must not exceed 10,000,000")
            .GreaterThanOrEqualTo(x => x.DailyLimit).WithMessage("Monthly limit must be greater than or equal to daily limit");

        RuleFor(x => x.ExpiresInDays)
            .GreaterThan(0).WithMessage("Expiration days must be greater than 0")
            .LessThanOrEqualTo(3650).WithMessage("Expiration days must not exceed 10 years");
    }
}

public class UpdateSubscriptionRequestValidator : AbstractValidator<UpdateSubscriptionRequest>
{
    public UpdateSubscriptionRequestValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters")
            .When(x => !string.IsNullOrEmpty(x.Name));

        RuleFor(x => x.DailyLimit)
            .GreaterThan(0).WithMessage("Daily limit must be greater than 0")
            .LessThanOrEqualTo(100000).WithMessage("Daily limit must not exceed 100,000")
            .When(x => x.DailyLimit.HasValue);

        RuleFor(x => x.MonthlyLimit)
            .GreaterThan(0).WithMessage("Monthly limit must be greater than 0")
            .LessThanOrEqualTo(10000000).WithMessage("Monthly limit must not exceed 10,000,000")
            .When(x => x.MonthlyLimit.HasValue);

        RuleFor(x => x.ExpiresAt)
            .GreaterThan(DateTime.UtcNow).WithMessage("Expiration date must be in the future")
            .When(x => x.ExpiresAt.HasValue);
    }
}
