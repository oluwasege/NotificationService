using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.Domain.Interfaces;
using NotificationService.Infrastructure.BackgroundServices;
using NotificationService.Infrastructure.Data;
using NotificationService.Infrastructure.Providers;
using NotificationService.Infrastructure.Queue;
using NotificationService.Infrastructure.Repositories;
using NotificationService.Infrastructure.Seeding;

namespace NotificationService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        services.AddDbContextFactory<NotificationDbContext>(
             options => options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"),
             p =>
             {
                 p.EnableRetryOnFailure(
                     maxRetryCount: 3,
                     maxRetryDelay: TimeSpan.FromSeconds(30),
                     errorNumbersToAdd: null);
                 p.MaxBatchSize(1500);
             }),
             ServiceLifetime.Scoped);

        // Repositories
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Notification Queue
        services.AddSingleton<INotificationQueue, InMemoryNotificationQueue>();

        // Notification Providers
        services.AddScoped<MockEmailProvider>();
        services.AddScoped<MockSmsProvider>();
        services.AddScoped<NotificationProviderFactory>();

        // Seeder
        services.AddScoped<DatabaseSeeder>();

        // Background Services
        services.AddHostedService<NotificationProcessorService>();
        services.AddHostedService<ScheduledNotificationService>();

        return services;
    }
}
