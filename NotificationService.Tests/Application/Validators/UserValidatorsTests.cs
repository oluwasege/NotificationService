using FluentAssertions;
using NotificationService.Application.DTOs;
using NotificationService.Application.Validators;
using NotificationService.Domain.Enums;

namespace NotificationService.Tests.Application.Validators;

public class UserValidatorsTests
{
    private readonly CreateUserRequestValidator _createValidator;
    private readonly UpdateUserRequestValidator _updateValidator;
    private readonly LoginRequestValidator _loginValidator;

    public UserValidatorsTests()
    {
        _createValidator = new CreateUserRequestValidator();
        _updateValidator = new UpdateUserRequestValidator();
        _loginValidator = new LoginRequestValidator();
    }

    [Fact]
    public async Task CreateUserRequestValidator_WithValidData_Passes()
    {
        // Arrange
        var request = new CreateUserRequest(
            "Test User",
            "test@example.com",
            "Password123!",
            UserRole.User
        );

        // Act
        var result = await _createValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task CreateUserRequestValidator_WithEmptyName_Fails()
    {
        // Arrange
        var request = new CreateUserRequest(
            "",
            "test@example.com",
            "Password123!",
            UserRole.User
        );

        // Act
        var result = await _createValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public async Task CreateUserRequestValidator_WithInvalidEmail_Fails()
    {
        // Arrange
        var request = new CreateUserRequest(
            "Test User",
            "invalid-email",
            "Password123!",
            UserRole.User
        );

        // Act
        var result = await _createValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public async Task CreateUserRequestValidator_WithWeakPassword_Fails()
    {
        // Arrange
        var request = new CreateUserRequest(
            "Test User",
            "test@example.com",
            "weak",
            UserRole.User
        );

        // Act
        var result = await _createValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }

    [Fact]
    public async Task CreateUserRequestValidator_WithPasswordMissingUppercase_Fails()
    {
        // Arrange
        var request = new CreateUserRequest(
            "Test User",
            "test@example.com",
            "password123!",
            UserRole.User
        );

        // Act
        var result = await _createValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("uppercase"));
    }

    [Fact]
    public async Task CreateUserRequestValidator_WithPasswordMissingSpecialChar_Fails()
    {
        // Arrange
        var request = new CreateUserRequest(
            "Test User",
            "test@example.com",
            "Password123",
            UserRole.User
        );

        // Act
        var result = await _createValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("special character"));
    }

    [Fact]
    public async Task UpdateUserRequestValidator_WithValidData_Passes()
    {
        // Arrange
        var request = new UpdateUserRequest(
            Name: "Updated Name",
            Email: "updated@example.com"
        );

        // Act
        var result = await _updateValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateUserRequestValidator_WithInvalidEmail_Fails()
    {
        // Arrange
        var request = new UpdateUserRequest(Email: "invalid-email");

        // Act
        var result = await _updateValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public async Task LoginRequestValidator_WithValidData_Passes()
    {
        // Arrange
        var request = new LoginRequest("test@example.com", "password");

        // Act
        var result = await _loginValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task LoginRequestValidator_WithEmptyEmail_Fails()
    {
        // Arrange
        var request = new LoginRequest("", "password");

        // Act
        var result = await _loginValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public async Task LoginRequestValidator_WithEmptyPassword_Fails()
    {
        // Arrange
        var request = new LoginRequest("test@example.com", "");

        // Act
        var result = await _loginValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }
}
