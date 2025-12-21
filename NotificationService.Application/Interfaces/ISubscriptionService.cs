using NotificationService.Application.DTOs;

namespace NotificationService.Application.Interfaces;

public interface ISubscriptionService
{
    Task<PagedResult<SubscriptionDto>> GetSubscriptionsAsync(
        Guid? userId = null, 
        int page = 1, 
        int pageSize = 20, 
        CancellationToken cancellationToken = default);
    
    Task<SubscriptionDto?> GetSubscriptionByIdAsync(
        Guid subscriptionId, 
        CancellationToken cancellationToken = default);
    
    Task<SubscriptionDto> CreateSubscriptionAsync(
        CreateSubscriptionRequest request, 
        CancellationToken cancellationToken = default);
    
    Task<SubscriptionDto?> UpdateSubscriptionAsync(
        Guid subscriptionId, 
        UpdateSubscriptionRequest request, 
        CancellationToken cancellationToken = default);
    
    Task<RegenerateKeyResponse?> RegenerateKeyAsync(
        Guid subscriptionId, 
        CancellationToken cancellationToken = default);
    
    Task<bool> DeleteSubscriptionAsync(
        Guid subscriptionId, 
        CancellationToken cancellationToken = default);
}
