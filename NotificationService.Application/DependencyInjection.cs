using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Services;
using NotificationService.Application.Validators;

namespace NotificationService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<ISubscriptionValidationService, SubscriptionValidationService>();
        services.AddScoped<INotificationService, NotificationAppService>();
        services.AddScoped<IDashboardService, DashboardService>();

        // Validators
        services.AddValidatorsFromAssemblyContaining<SendNotificationRequestValidator>();

        return services;
    }
}
