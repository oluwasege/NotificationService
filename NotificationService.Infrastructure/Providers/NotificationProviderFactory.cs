using NotificationService.Domain.Enums;
using NotificationService.Domain.Interfaces;

namespace NotificationService.Infrastructure.Providers;

public class NotificationProviderFactory
{
    private readonly IServiceProvider _serviceProvider;

    public NotificationProviderFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public INotificationProvider GetProvider(NotificationType type)
    {
        return type switch
        {
            NotificationType.Email => (INotificationProvider)_serviceProvider.GetService(typeof(MockEmailProvider))!,
            NotificationType.Sms => (INotificationProvider)_serviceProvider.GetService(typeof(MockSmsProvider))!,
            _ => throw new ArgumentOutOfRangeException(nameof(type), $"No provider registered for {type}")
        };
    }
}
