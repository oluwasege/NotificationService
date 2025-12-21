using FluentAssertions;
using NotificationService.Application.DTOs;
using NotificationService.Application.Validators;
using NotificationService.Domain.Enums;

namespace NotificationService.Tests.Application;

public class UserValidatorsTests
{
    private readonly CreateUserRequestValidator _createUserValidator;
    private readonly UpdateUserRequestValidator _updateUserValidator;
    private readonly LoginRequestValidator _loginValidator;

    public UserValidatorsTests()
    {
        _createUserValidator = new CreateUserRequestValidator();
        _updateUserValidator = new UpdateUserRequestValidator();
        _loginValidator = new LoginRequestValidator();
    }

    [Fact]
    public async Task CreateUserRequest_ValidRequest_PassesValidation()
    {
        // Arrange
        var request = new CreateUserRequest(
            "John Doe",
            "john@example.com",
            "SecurePass123!",
            UserRole.User
        );

        // Act
        var result = await _createUserValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task CreateUserRequest_EmptyName_FailsValidation()
    {
        // Arrange
        var request = new CreateUserRequest(
            "",
            "john@example.com",
            "SecurePass123!",
            UserRole.User
        );

        // Act
        var result = await _createUserValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public async Task CreateUserRequest_InvalidEmail_FailsValidation()
    {
        // Arrange
        var request = new CreateUserRequest(
            "John Doe",
            "invalid-email",
            "SecurePass123!",
            UserRole.User
        );

        // Act
        var result = await _createUserValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public async Task CreateUserRequest_WeakPassword_FailsValidation()
    {
        // Arrange
        var request = new CreateUserRequest(
            "John Doe",
            "john@example.com",
            "weak",
            UserRole.User
        );

        // Act
        var result = await _createUserValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }

    [Fact]
    public async Task CreateUserRequest_PasswordWithoutUppercase_FailsValidation()
    {
        // Arrange
        var request = new CreateUserRequest(
            "John Doe",
            "john@example.com",
            "securepass123!",
            UserRole.User
        );

        // Act
        var result = await _createUserValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password" && e.ErrorMessage.Contains("uppercase"));
    }

    [Fact]
    public async Task CreateUserRequest_PasswordWithoutDigit_FailsValidation()
    {
        // Arrange
        var request = new CreateUserRequest(
            "John Doe",
            "john@example.com",
            "SecurePass!",
            UserRole.User
        );

        // Act
        var result = await _createUserValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password" && e.ErrorMessage.Contains("digit"));
    }

    [Fact]
    public async Task UpdateUserRequest_ValidRequest_PassesValidation()
    {
        // Arrange
        var request = new UpdateUserRequest(
            "Jane Doe",
            "jane@example.com",
            UserRole.Admin,
            true
        );

        // Act
        var result = await _updateUserValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateUserRequest_InvalidEmail_FailsValidation()
    {
        // Arrange
        var request = new UpdateUserRequest(
            Email: "invalid-email"
        );

        // Act
        var result = await _updateUserValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public async Task LoginRequest_ValidCredentials_PassesValidation()
    {
        // Arrange
        var request = new LoginRequest(
            "admin@example.com",
            "password123"
        );

        // Act
        var result = await _loginValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task LoginRequest_EmptyEmail_FailsValidation()
    {
        // Arrange
        var request = new LoginRequest(
            "",
            "password123"
        );

        // Act
        var result = await _loginValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public async Task LoginRequest_EmptyPassword_FailsValidation()
    {
        // Arrange
        var request = new LoginRequest(
            "admin@example.com",
            ""
        );

        // Act
        var result = await _loginValidator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }
}
