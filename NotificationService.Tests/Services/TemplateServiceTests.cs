using FakeItEasy;
using Microsoft.Extensions.Logging;
using NotificationService.Application.DTOs;
using NotificationService.Application.Services;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Interfaces;
using NotificationService.Tests.Helpers;
using System.Linq.Expressions;

namespace NotificationService.Tests.Services;

public class TemplateServiceTests
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TemplateService> _logger;
    private readonly TemplateService _templateService;
    private readonly IRepository<NotificationTemplate> _templateRepository;

    public TemplateServiceTests()
    {
        _unitOfWork = A.Fake<IUnitOfWork>();
        _logger = A.Fake<ILogger<TemplateService>>();
        _templateRepository = A.Fake<IRepository<NotificationTemplate>>();

        A.CallTo(() => _unitOfWork.GetRepository<NotificationTemplate>()).Returns(_templateRepository);

        _templateService = new TemplateService(_unitOfWork, _logger);
    }

    [Fact]
    public async Task RenderAsync_WithValidTemplate_ReturnsRenderedString()
    {
        // Arrange
        var template = "Hello {{ name }}!";
        var data = new Dictionary<string, object> { { "name", "World" } };

        // Act
        var result = await _templateService.RenderAsync(template, data);

        // Assert
        Assert.Equal("Hello World!", result);
    }

    [Fact]
    public async Task RenderAsync_WithInvalidTemplate_ThrowsInvalidOperationException()
    {
        // Arrange
        var template = "Hello {{ if true }} missing end"; // Invalid syntax
        var data = new Dictionary<string, object> { { "name", "World" } };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _templateService.RenderAsync(template, data));
    }

    [Fact]
    public async Task CreateTemplateAsync_WithValidRequest_ReturnsTemplateDto()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var request = new CreateTemplateRequest(
            "Test Template",
            "Description",
            NotificationType.Email,
            "Subject {{ name }}",
            "Body {{ name }}"
        );

        A.CallTo(() => _templateRepository.AddAsync(A<NotificationTemplate>._, A<CancellationToken>._))
            .Invokes((NotificationTemplate t, CancellationToken _) => t.Id = Guid.NewGuid());

        // Act
        var result = await _templateService.CreateTemplateAsync(subscriptionId, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(request.Name, result.Name);
        Assert.Equal(request.SubjectTemplate, result.SubjectTemplate);
        
        A.CallTo(() => _templateRepository.AddAsync(A<NotificationTemplate>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetTemplatesAsync_ReturnsTemplatesForSubscription()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var templates = new List<NotificationTemplate>
        {
            new() { Id = Guid.NewGuid(), SubscriptionId = subscriptionId, Name = "T1", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), SubscriptionId = subscriptionId, Name = "T2", CreatedAt = DateTime.UtcNow.AddMinutes(-1) }
        };

        A.CallTo(() => _templateRepository.QueryNoTracking())
            .Returns(MockAsyncQueryable.Build(templates));

        // Act
        var result = await _templateService.GetTemplatesAsync(subscriptionId);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("T1", result[0].Name);
    }

    [Fact]
    public async Task GetTemplateByIdAsync_WithExistingId_ReturnsTemplate()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var template = new NotificationTemplate { Id = templateId, Name = "Test" };

        A.CallTo(() => _templateRepository.GetByIdAsync(templateId, A<CancellationToken>._))
            .Returns(template);

        // Act
        var result = await _templateService.GetTemplateByIdAsync(templateId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(templateId, result.Id);
    }

    [Fact]
    public async Task UpdateTemplateAsync_WithValidRequest_UpdatesTemplate()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var template = new NotificationTemplate { Id = templateId, Name = "Old Name" };
        var request = new UpdateTemplateRequest { Name = "New Name" };

        A.CallTo(() => _templateRepository.GetByIdAsync(templateId, A<CancellationToken>._))
            .Returns(template);

        // Act
        var result = await _templateService.UpdateTemplateAsync(templateId, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("New Name", result.Name);
        
        A.CallTo(() => _templateRepository.UpdateAsync(template, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task DeleteTemplateAsync_WithExistingId_ReturnsTrue()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var template = new NotificationTemplate { Id = templateId };

        A.CallTo(() => _templateRepository.GetByIdAsync(templateId, A<CancellationToken>._))
            .Returns(template);

        // Act
        var result = await _templateService.DeleteTemplateAsync(templateId);

        // Assert
        Assert.True(result);
        
        A.CallTo(() => _templateRepository.SoftDeleteAsync(template, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }
}
