using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Services;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Interfaces;
using NotificationService.Tests.Helpers;

namespace NotificationService.Tests.Application.Services;

public class UserServiceTests
{
    private readonly Mock<IRepository<User>> _userRepositoryMock;
    private readonly Mock<IRepository<Subscription>> _subscriptionRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IAuthService> _authServiceMock;
    private readonly Mock<ILogger<UserService>> _loggerMock;
    private readonly UserService _service;

    public UserServiceTests()
    {
        _userRepositoryMock = new Mock<IRepository<User>>();
        _subscriptionRepositoryMock = new Mock<IRepository<Subscription>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _authServiceMock = new Mock<IAuthService>();
        _loggerMock = new Mock<ILogger<UserService>>();

        _service = new UserService(
            _userRepositoryMock.Object,
            _subscriptionRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _authServiceMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task CreateUserAsync_WithValidData_CreatesUserSuccessfully()
    {
        // Arrange
        var request = TestDataFactory.CreateUserRequest();
        var hashedPassword = "hashed_password";

        _userRepositoryMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _authServiceMock
            .Setup(x => x.HashPassword(request.Password))
            .Returns(hashedPassword);

        _unitOfWorkMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.CreateUserAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be(request.Name);
        result.Email.Should().Be(request.Email);
        result.Role.Should().Be(request.Role);
        result.IsActive.Should().BeTrue();

        _userRepositoryMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateUserAsync_WithDuplicateEmail_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = TestDataFactory.CreateUserRequest();
        var existingUser = TestDataFactory.CreateTestUser(email: request.Email);

        _userRepositoryMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateUserAsync(request));

        _userRepositoryMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateUserAsync_WithValidData_UpdatesUserSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingUser = TestDataFactory.CreateTestUser(id: userId);
        var request = new UpdateUserRequest(Name: "Updated Name");

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        _unitOfWorkMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.UpdateUserAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Updated Name");

        _userRepositoryMock.Verify(x => x.UpdateAsync(existingUser, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateUserAsync_WithNonExistentUser_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new UpdateUserRequest(Name: "Updated Name");

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _service.UpdateUserAsync(userId, request);

        // Assert
        result.Should().BeNull();
        _userRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateUserAsync_WithDuplicateEmail_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingUser = TestDataFactory.CreateTestUser(id: userId);
        var otherUser = TestDataFactory.CreateTestUser(email: "other@example.com");
        var request = new UpdateUserRequest(Email: "other@example.com");

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        _userRepositoryMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherUser);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.UpdateUserAsync(userId, request));
    }

    [Fact]
    public async Task DeleteUserAsync_WithExistingUser_DeletesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingUser = TestDataFactory.CreateTestUser(id: userId);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        _unitOfWorkMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.DeleteUserAsync(userId);

        // Assert
        result.Should().BeTrue();
        _userRepositoryMock.Verify(x => x.SoftDeleteAsync(existingUser, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteUserAsync_WithNonExistentUser_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _service.DeleteUserAsync(userId);

        // Assert
        result.Should().BeFalse();
        _userRepositoryMock.Verify(x => x.SoftDeleteAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
