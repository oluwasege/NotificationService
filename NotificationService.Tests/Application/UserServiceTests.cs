using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Services;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Interfaces;

namespace NotificationService.Tests.Application;

public class UserServiceTests
{
    private readonly Mock<IRepository<User>> _userRepoMock;
    private readonly Mock<IRepository<Subscription>> _subscriptionRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IAuthService> _authServiceMock;
    private readonly Mock<ILogger<UserService>> _loggerMock;
    private readonly UserService _service;

    public UserServiceTests()
    {
        _userRepoMock = new Mock<IRepository<User>>();
        _subscriptionRepoMock = new Mock<IRepository<Subscription>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _authServiceMock = new Mock<IAuthService>();
        _loggerMock = new Mock<ILogger<UserService>>();

        _service = new UserService(
            _userRepoMock.Object,
            _subscriptionRepoMock.Object,
            _unitOfWorkMock.Object,
            _authServiceMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task CreateUserAsync_ValidRequest_ReturnsUserDto()
    {
        // Arrange
        var request = new CreateUserRequest(
            "Test User",
            "test@example.com",
            "password123",
            UserRole.Admin
        );

        _authServiceMock
            .Setup(x => x.HashPassword(request.Password))
            .Returns("hashed_password");

        _userRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _service.CreateUserAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be(request.Name);
        result.Email.Should().Be(request.Email);
        result.Role.Should().Be(request.Role);
        result.IsActive.Should().BeTrue();

        _userRepoMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateUserAsync_DuplicateEmail_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = new CreateUserRequest(
            "Test User",
            "test@example.com",
            "password123",
            UserRole.Admin
        );

        var existingUser = new User { Email = "test@example.com" };

        _userRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.CreateUserAsync(request));
    }

    [Fact]
    public async Task UpdateUserAsync_ValidRequest_ReturnsUpdatedUserDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Name = "Old Name",
            Email = "old@example.com",
            Role = UserRole.User,
            IsActive = true
        };

        var request = new UpdateUserRequest(
            "New Name",
            "new@example.com",
            UserRole.Admin,
            false
        );

        _userRepoMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _userRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _service.UpdateUserAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be(request.Name);
        result.Email.Should().Be(request.Email);
        result.Role.Should().Be(request.Role.Value);
        result.IsActive.Should().Be(request.IsActive.Value);

        _userRepoMock.Verify(x => x.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateUserAsync_UserNotFound_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new UpdateUserRequest(Name: "New Name");

        _userRepoMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _service.UpdateUserAsync(userId, request);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateUserAsync_DuplicateEmail_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "current@example.com"
        };

        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "existing@example.com"
        };

        var request = new UpdateUserRequest(
            Email: "existing@example.com"
        );

        _userRepoMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _userRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.UpdateUserAsync(userId, request));
    }

    [Fact]
    public async Task DeleteUserAsync_ValidUser_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId };

        _userRepoMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _service.DeleteUserAsync(userId);

        // Assert
        result.Should().BeTrue();
        _userRepoMock.Verify(x => x.SoftDeleteAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteUserAsync_UserNotFound_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _userRepoMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _service.DeleteUserAsync(userId);

        // Assert
        result.Should().BeFalse();
    }
}
