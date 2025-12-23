using Microsoft.Extensions.Diagnostics.HealthChecks;
using NotificationService.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotificationService.Api.Middleware
{
    /// <summary>
    /// Custom health check for the notification queue.
    /// </summary>
    public class NotificationQueueHealthCheck : IHealthCheck
    {
        private readonly INotificationQueue _queue;

        public NotificationQueueHealthCheck(INotificationQueue queue)
        {
            _queue = queue;
        }

        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            var queueDepth = _queue.GetQueueCount();

            if (queueDepth > 10000)
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"Queue depth is high: {queueDepth}"));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                $"Queue depth: {queueDepth}"));
        }
    }
}
