using NotificationService.Application.DTOs;

namespace NotificationService.Application.Interfaces;

public interface ISubscriptionValidationService
{
    Task<SubscriptionKeyValidationResult> ValidateSubscriptionKeyAsync(
        string subscriptionKey, 
        CancellationToken cancellationToken = default);
    
    Task<bool> CanSendNotificationAsync(
        Guid subscriptionId, 
        Domain.Enums.NotificationType type, 
        CancellationToken cancellationToken = default);
    
    Task IncrementUsageAsync(
        Guid subscriptionId, 
        CancellationToken cancellationToken = default);
}
