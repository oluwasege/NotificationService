using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using NotificationService.Application.DTOs;
using NotificationService.Application.Services;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Interfaces;
using NotificationService.Tests.Helpers;

namespace NotificationService.Tests.Application.Services;

public class AuthServiceTests
{
    private readonly Mock<IRepository<User>> _userRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<AuthService>> _loggerMock;
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _userRepositoryMock = new Mock<IRepository<User>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _configurationMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<AuthService>>();

        // Setup configuration
        _configurationMock.Setup(c => c["Jwt:Secret"])
            .Returns("YourSuperSecretKeyThatIsAtLeast32CharactersLong!");
        _configurationMock.Setup(c => c["Jwt:Issuer"])
            .Returns("NotificationService");
        _configurationMock.Setup(c => c["Jwt:Audience"])
            .Returns("NotificationServiceClients");
        _configurationMock.Setup(c => c["Jwt:ExpiresInMinutes"])
            .Returns("60");
        _configurationMock.Setup(c => c["Security:PasswordSalt"])
            .Returns("NotificationServiceSalt2024");

        _service = new AuthService(
            _userRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _configurationMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsLoginResponse()
    {
        // Arrange
        var password = "TestPassword123!";
        var hashedPassword = _service.HashPassword(password);
        var user = TestDataFactory.CreateTestUser(
            email: "test@example.com",
            isActive: true
        );
        user.PasswordHash = hashedPassword;

        var request = new LoginRequest("test@example.com", password);

        _userRepositoryMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _unitOfWorkMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.LoginAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.Token.Should().NotBeNullOrEmpty();
        result.TokenType.Should().Be("Bearer");
        result.ExpiresIn.Should().Be(3600); // 60 minutes * 60 seconds
        result.User.Email.Should().Be(user.Email);

        _userRepositoryMock.Verify(x => x.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        user.LastLoginAt.Should().NotBeNull();
    }

    [Fact]
    public async Task LoginAsync_WithInvalidEmail_ReturnsNull()
    {
        // Arrange
        var request = new LoginRequest("nonexistent@example.com", "password");

        _userRepositoryMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _service.LoginAsync(request);

        // Assert
        result.Should().BeNull();
        _userRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WithInvalidPassword_ReturnsNull()
    {
        // Arrange
        var user = TestDataFactory.CreateTestUser(email: "test@example.com");
        user.PasswordHash = _service.HashPassword("CorrectPassword");

        var request = new LoginRequest("test@example.com", "WrongPassword");

        _userRepositoryMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _service.LoginAsync(request);

        // Assert
        result.Should().BeNull();
        _userRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WithInactiveUser_ReturnsNull()
    {
        // Arrange
        var password = "TestPassword123!";
        var hashedPassword = _service.HashPassword(password);
        var user = TestDataFactory.CreateTestUser(
            email: "test@example.com",
            isActive: false
        );
        user.PasswordHash = hashedPassword;

        var request = new LoginRequest("test@example.com", password);

        _userRepositoryMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _service.LoginAsync(request);

        // Assert
        result.Should().BeNull();
        _userRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetCurrentUserAsync_WithValidUserId_ReturnsUserDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = TestDataFactory.CreateTestUser(id: userId);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _service.GetCurrentUserAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(userId);
        result.Email.Should().Be(user.Email);
        result.Name.Should().Be(user.Name);
    }

    [Fact]
    public async Task GetCurrentUserAsync_WithInvalidUserId_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _service.GetCurrentUserAsync(userId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void HashPassword_WithValidPassword_ReturnsHashedString()
    {
        // Arrange
        var password = "TestPassword123!";

        // Act
        var result = _service.HashPassword(password);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().NotBe(password);
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ReturnsTrue()
    {
        // Arrange
        var password = "TestPassword123!";
        var hash = _service.HashPassword(password);

        // Act
        var result = _service.VerifyPassword(password, hash);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_WithIncorrectPassword_ReturnsFalse()
    {
        // Arrange
        var password = "TestPassword123!";
        var hash = _service.HashPassword(password);

        // Act
        var result = _service.VerifyPassword("WrongPassword", hash);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HashPassword_WithSamePassword_ProducesSameHash()
    {
        // Arrange
        var password = "TestPassword123!";

        // Act
        var hash1 = _service.HashPassword(password);
        var hash2 = _service.HashPassword(password);

        // Assert
        hash1.Should().Be(hash2);
    }
}
