using FluentAssertions;
using NotificationService.Application.DTOs;
using NotificationService.Application.Validators;
using NotificationService.Domain.Enums;

namespace NotificationService.Tests.Application.Validators;

public class SubscriptionValidatorsTests
{
    private readonly CreateSubscriptionRequestValidator _createValidator;
    private readonly UpdateSubscriptionRequestValidator _updateValidator;

    public SubscriptionValidatorsTests()
    {
        _createValidator = new CreateSubscriptionRequestValidator();
        _updateValidator = new UpdateSubscriptionRequestValidator();
    }

    [Fact]
    public async Task CreateSubscriptionRequestValidator_WithValidData_Passes()
    {
        // Arrange
        var request = new CreateSubscriptionRequest(
            Guid.NewGuid(),
            "Test Subscription",
            1000,
            30000,
            365,
            true,
            true
        );

        // Act
        var result = await _createValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task CreateSubscriptionRequestValidator_WithEmptyUserId_Fails()
    {
        // Arrange
        var request = new CreateSubscriptionRequest(
            Guid.Empty,
            "Test Subscription",
            1000,
            30000,
            365,
            true,
            true
        );

        // Act
        var result = await _createValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "UserId");
    }

    [Fact]
    public async Task CreateSubscriptionRequestValidator_WithEmptyName_Fails()
    {
        // Arrange
        var request = new CreateSubscriptionRequest(
            Guid.NewGuid(),
            "",
            1000,
            30000,
            365,
            true,
            true
        );

        // Act
        var result = await _createValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public async Task CreateSubscriptionRequestValidator_WithInvalidDailyLimit_Fails()
    {
        // Arrange
        var request = new CreateSubscriptionRequest(
            Guid.NewGuid(),
            "Test Subscription",
            0,
            30000,
            365,
            true,
            true
        );

        // Act
        var result = await _createValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DailyLimit");
    }

    [Fact]
    public async Task CreateSubscriptionRequestValidator_WithMonthlyLimitLessThanDaily_Fails()
    {
        // Arrange
        var request = new CreateSubscriptionRequest(
            Guid.NewGuid(),
            "Test Subscription",
            1000,
            500,
            365,
            true,
            true
        );

        // Act
        var result = await _createValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MonthlyLimit");
    }

    [Fact]
    public async Task CreateSubscriptionRequestValidator_WithExcessiveExpirationDays_Fails()
    {
        // Arrange
        var request = new CreateSubscriptionRequest(
            Guid.NewGuid(),
            "Test Subscription",
            1000,
            30000,
            3651,
            true,
            true
        );

        // Act
        var result = await _createValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ExpiresInDays");
    }

    [Fact]
    public async Task UpdateSubscriptionRequestValidator_WithValidData_Passes()
    {
        // Arrange
        var request = new UpdateSubscriptionRequest(
            Name: "Updated Subscription",
            Status: SubscriptionStatus.Active,
            DailyLimit: 2000,
            MonthlyLimit: 60000
        );

        // Act
        var result = await _updateValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateSubscriptionRequestValidator_WithInvalidDailyLimit_Fails()
    {
        // Arrange
        var request = new UpdateSubscriptionRequest(DailyLimit: 0);

        // Act
        var result = await _updateValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DailyLimit");
    }

    [Fact]
    public async Task UpdateSubscriptionRequestValidator_WithPastExpirationDate_Fails()
    {
        // Arrange
        var request = new UpdateSubscriptionRequest(ExpiresAt: DateTime.UtcNow.AddDays(-1));

        // Act
        var result = await _updateValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ExpiresAt");
    }

    [Fact]
    public async Task UpdateSubscriptionRequestValidator_WithFutureExpirationDate_Passes()
    {
        // Arrange
        var request = new UpdateSubscriptionRequest(ExpiresAt: DateTime.UtcNow.AddDays(30));

        // Act
        var result = await _updateValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
