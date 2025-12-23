using FakeItEasy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NotificationService.Application.DTOs;
using NotificationService.Application.Services;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Interfaces;
using System.Linq.Expressions;

namespace NotificationService.Tests.Services;

public class AuthServiceTests
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;
    private readonly AuthService _authService;
    private readonly IRepository<User> _userRepository;

    public AuthServiceTests()
    {
        _unitOfWork = A.Fake<IUnitOfWork>();
        _configuration = A.Fake<IConfiguration>();
        _logger = A.Fake<ILogger<AuthService>>();
        _userRepository = A.Fake<IRepository<User>>();

        A.CallTo(() => _unitOfWork.GetRepository<User>()).Returns(_userRepository);
        
        // Setup default configuration values
        A.CallTo(() => _configuration["Jwt:Secret"]).Returns("SuperSecretKeyThatIsLongEnoughForTesting123!");
        A.CallTo(() => _configuration["Jwt:Issuer"]).Returns("TestIssuer");
        A.CallTo(() => _configuration["Jwt:Audience"]).Returns("TestAudience");
        A.CallTo(() => _configuration["Jwt:ExpiresInMinutes"]).Returns("60");
        A.CallTo(() => _configuration["Security:PasswordSalt"]).Returns("TestSalt");

        _authService = new AuthService(_unitOfWork, _configuration, _logger);
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsLoginResponse()
    {
        // Arrange
        var password = "Password123";
        var hashedPassword = _authService.HashPassword(password);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = hashedPassword,
            IsActive = true,
            Role = UserRole.User,
            Name = "Test User"
        };

        A.CallTo(() => _userRepository.FirstOrDefaultAsync(
            A<Expression<Func<User, bool>>>._, 
            A<CancellationToken>._))
            .Returns(Task.FromResult(user)); 

        var request = new LoginRequest("test@example.com", password);

        // Act
        var result = await _authService.LoginAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Token);
        Assert.Equal("Bearer", result.TokenType);
        Assert.Equal(user.Email, result.User.Email);
        
        A.CallTo(() => _userRepository.UpdateAsync(user, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task LoginAsync_WithInvalidEmail_ReturnsNull()
    {
        // Arrange
        A.CallTo(() => _userRepository.FirstOrDefaultAsync(
            A<Expression<Func<User, bool>>>._, 
            A<CancellationToken>._))
            .Returns(Task.FromResult<User>(null!)); 

        var request = new LoginRequest("nonexistent@example.com", "Password123");

        // Act
        var result = await _authService.LoginAsync(request);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task LoginAsync_WithInvalidPassword_ReturnsNull()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = _authService.HashPassword("CorrectPassword"),
            IsActive = true
        };

        A.CallTo(() => _userRepository.FirstOrDefaultAsync(
            A<Expression<Func<User, bool>>>._, 
            A<CancellationToken>._))
            .Returns(Task.FromResult(user)); 

        var request = new LoginRequest("test@example.com", "WrongPassword");

        // Act
        var result = await _authService.LoginAsync(request);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task LoginAsync_WithInactiveUser_ReturnsNull()
    {
        // Arrange
        var password = "Password123";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = _authService.HashPassword(password),
            IsActive = false
        };

        A.CallTo(() => _userRepository.FirstOrDefaultAsync(
            A<Expression<Func<User, bool>>>._, 
            A<CancellationToken>._))
            .Returns(Task.FromResult(user)); 

        var request = new LoginRequest("test@example.com", password);

        // Act
        var result = await _authService.LoginAsync(request);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetCurrentUserAsync_WithValidId_ReturnsUserDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            Name = "Test User",
            Role = UserRole.User,
            IsActive = true
        };

        A.CallTo(() => _userRepository.GetByIdAsync(userId, A<CancellationToken>._))
            .Returns(Task.FromResult(user!));

        // Act
        var result = await _authService.GetCurrentUserAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.Id);
        Assert.Equal(user.Email, result.Email);
    }

    [Fact]
    public async Task GetCurrentUserAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        A.CallTo(() => _userRepository.GetByIdAsync(userId, A<CancellationToken>._))
            .Returns(Task.FromResult<User>(null!)); 

        // Act
        var result = await _authService.GetCurrentUserAsync(userId);

        // Assert
        Assert.Null(result);
    }
}
