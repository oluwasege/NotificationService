using FakeItEasy;
using Microsoft.Extensions.Logging;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Services;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Interfaces;
using NotificationService.Tests.Helpers;
using System.Linq.Expressions;

namespace NotificationService.Tests.Services;

public class UserServiceTests
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthService _authService;
    private readonly ILogger<UserService> _logger;
    private readonly UserService _userService;
    private readonly IRepository<User> _userRepository;

    public UserServiceTests()
    {
        _unitOfWork = A.Fake<IUnitOfWork>();
        _authService = A.Fake<IAuthService>();
        _logger = A.Fake<ILogger<UserService>>();
        _userRepository = A.Fake<IRepository<User>>();

        A.CallTo(() => _unitOfWork.GetRepository<User>()).Returns(_userRepository);

        _userService = new UserService(_unitOfWork, _authService, _logger);
    }

    [Fact]
    public async Task CreateUserAsync_WithValidRequest_ReturnsUser()
    {
        // Arrange
        var request = new CreateUserRequest(
            "Test User",
            "test@example.com",
            "Password123",
            UserRole.User
        );

        A.CallTo(() => _userRepository.FirstOrDefaultAsync(
            A<Expression<Func<User, bool>>>._, A<CancellationToken>._))
            .Returns(Task.FromResult<User>(null!));

        A.CallTo(() => _authService.HashPassword(request.Password))
            .Returns("hashed_password");

        // Act
        var result = await _userService.CreateUserAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(request.Name, result.Name);
        Assert.Equal(request.Email, result.Email);
        Assert.Equal(request.Role, result.Role);

        A.CallTo(() => _userRepository.AddAsync(A<User>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task CreateUserAsync_WithExistingEmail_ThrowsException()
    {
        // Arrange
        var request = new CreateUserRequest(
            "Test User",
            "existing@example.com",
            "Password123",
            UserRole.User
        );

        var existingUser = new User { Email = "existing@example.com" };

        A.CallTo(() => _userRepository.FirstOrDefaultAsync(
            A<Expression<Func<User, bool>>>._, A<CancellationToken>._))
            .Returns(Task.FromResult(existingUser!));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _userService.CreateUserAsync(request));
    }

    [Fact]
    public async Task GetUserByIdAsync_WithValidId_ReturnsUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Name = "Test User",
            Email = "test@example.com",
            Subscriptions = []
        };

        A.CallTo(() => _userRepository.QueryNoTracking())
            .Returns(MockAsyncQueryable.Build([user]));

        A.CallTo(() => _userRepository.FirstOrDefaultAsync(
            A<Expression<Func<User, bool>>>._, A<CancellationToken>._))
            .Returns(Task.FromResult(user!));

        // Act
        var result = await _userService.GetUserByIdAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.Id);
        Assert.Equal(user.Email, result.Email);
    }

    [Fact]
    public async Task UpdateUserAsync_WithValidId_UpdatesAndReturnsUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Name = "Old Name",
            Email = "old@example.com"
        };

        A.CallTo(() => _userRepository.GetByIdAsync(userId, A<CancellationToken>._))
            .Returns(user);

        var request = new UpdateUserRequest(
            "New Name",
            "new@example.com",
            UserRole.Admin,
            false
        );

        A.CallTo(() => _userRepository.FirstOrDefaultAsync(
            A<Expression<Func<User, bool>>>._, A<CancellationToken>._))
            .Returns(Task.FromResult<User>(null!));

        // Act
        var result = await _userService.UpdateUserAsync(userId, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("New Name", result.Name);
        Assert.Equal("new@example.com", result.Email);
        Assert.Equal(UserRole.Admin, result.Role);
        Assert.False(result.IsActive);

        A.CallTo(() => _userRepository.UpdateAsync(user, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task DeleteUserAsync_WithValidId_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId };

        A.CallTo(() => _userRepository.GetByIdAsync(userId, A<CancellationToken>._))
            .Returns(user);

        // Act
        var result = await _userService.DeleteUserAsync(userId);

        // Assert
        Assert.True(result);

        A.CallTo(() => _userRepository.SoftDeleteAsync(user, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }
}
