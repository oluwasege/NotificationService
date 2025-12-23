using FakeItEasy;
using Microsoft.Extensions.Logging;
using NotificationService.Application.DTOs;
using NotificationService.Application.Services;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Interfaces;
using NotificationService.Tests.Helpers;
using System.Net;

namespace NotificationService.Tests.Services;

public class WebhookServiceTests
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<WebhookService> _logger;
    private readonly WebhookService _webhookService;
    private readonly IRepository<WebhookSubscription> _webhookRepository;
    private readonly MockHttpMessageHandler _httpMessageHandler;

    public WebhookServiceTests()
    {
        _unitOfWork = A.Fake<IUnitOfWork>();
        _logger = A.Fake<ILogger<WebhookService>>();
        _webhookRepository = A.Fake<IRepository<WebhookSubscription>>();
        _httpMessageHandler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(_httpMessageHandler);

        A.CallTo(() => _unitOfWork.GetRepository<WebhookSubscription>()).Returns(_webhookRepository);

        _webhookService = new WebhookService(_unitOfWork, _logger, httpClient);
    }

    [Fact]
    public async Task CreateWebhookAsync_WithValidRequest_ReturnsWebhookDto()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var request = new CreateWebhookRequest(
            "Test Webhook",
            "https://example.com/webhook",
            "sent,failed",
            "secret"
        );

        A.CallTo(() => _webhookRepository.AddAsync(A<WebhookSubscription>._, A<CancellationToken>._))
            .Invokes((WebhookSubscription w, CancellationToken _) => w.Id = Guid.NewGuid());

        // Act
        var result = await _webhookService.CreateWebhookAsync(subscriptionId, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(request.Name, result.Name);
        Assert.Equal(request.Url, result.Url);
        
        A.CallTo(() => _webhookRepository.AddAsync(A<WebhookSubscription>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task SendWebhookAsync_WithMatchingEvent_SendsRequest()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var payload = new WebhookEventPayload(
            Guid.NewGuid(),
            NotificationStatus.Sent,
            NotificationType.Email,
            "test@example.com",
            DateTime.UtcNow,
            null,
            "ext-id"
        );

        var webhook = new WebhookSubscription
        {
            Id = Guid.NewGuid(),
            SubscriptionId = subscriptionId,
            Url = "https://example.com/webhook",
            Events = "Sent,Failed",
            Secret = "secret",
            IsActive = true
        };

        A.CallTo(() => _webhookRepository.QueryNoTracking())
            .Returns(MockAsyncQueryable.Build(new List<WebhookSubscription> { webhook }));

        A.CallTo(() => _webhookRepository.GetByIdAsync(webhook.Id, A<CancellationToken>._))
            .Returns(webhook);

        bool requestSent = false;
        _httpMessageHandler.SendAsyncFunc = (req, ct) =>
        {
            requestSent = true;
            Assert.Equal(webhook.Url, req.RequestUri?.ToString());
            Assert.True(req.Headers.Contains("X-Webhook-Signature"));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        };

        // Act
        await _webhookService.SendWebhookAsync(subscriptionId, payload);

        // Allow background task to complete
        await Task.Delay(100);

        // Assert
        Assert.True(requestSent);
        A.CallTo(() => _webhookRepository.UpdateAsync(webhook, A<CancellationToken>._))
            .MustHaveHappened(); // Success update
    }

    [Fact]
    public async Task SendWebhookAsync_WithNonMatchingEvent_DoesNotSendRequest()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var payload = new WebhookEventPayload(
            Guid.NewGuid(),
            NotificationStatus.Failed, // Event is Failed
            NotificationType.Email,
            "test@example.com",
            DateTime.UtcNow,
            "Error",
            null
        );

        var webhook = new WebhookSubscription
        {
            Id = Guid.NewGuid(),
            SubscriptionId = subscriptionId,
            Url = "https://example.com/webhook",
            Events = "Sent", // Only subscribed to Sent
            IsActive = true
        };

        A.CallTo(() => _webhookRepository.QueryNoTracking())
            .Returns(MockAsyncQueryable.Build(new List<WebhookSubscription> { webhook }));

        bool requestSent = false;
        _httpMessageHandler.SendAsyncFunc = (req, ct) =>
        {
            requestSent = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        };

        // Act
        await _webhookService.SendWebhookAsync(subscriptionId, payload);

        // Assert
        Assert.False(requestSent);
    }

    [Fact]
    public async Task GetWebhooksAsync_ReturnsWebhooksForSubscription()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var webhooks = new List<WebhookSubscription>
        {
            new() { Id = Guid.NewGuid(), SubscriptionId = subscriptionId, Name = "W1", CreatedAt = DateTime.UtcNow }
        };

        A.CallTo(() => _webhookRepository.QueryNoTracking())
            .Returns(MockAsyncQueryable.Build(webhooks));

        // Act
        var result = await _webhookService.GetWebhooksAsync(subscriptionId);

        // Assert
        Assert.Single(result);
        Assert.Equal("W1", result[0].Name);
    }

    [Fact]
    public async Task DeleteWebhookAsync_WithExistingId_ReturnsTrue()
    {
        // Arrange
        var webhookId = Guid.NewGuid();
        var webhook = new WebhookSubscription { Id = webhookId };

        A.CallTo(() => _webhookRepository.GetByIdAsync(webhookId, A<CancellationToken>._))
            .Returns(webhook);

        // Act
        var result = await _webhookService.DeleteWebhookAsync(webhookId);

        // Assert
        Assert.True(result);
        A.CallTo(() => _webhookRepository.SoftDeleteAsync(webhook, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }
}

public class MockHttpMessageHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? SendAsyncFunc { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (SendAsyncFunc != null)
        {
            return await SendAsyncFunc(request, cancellationToken);
        }
        return new HttpResponseMessage(HttpStatusCode.OK);
    }
}
