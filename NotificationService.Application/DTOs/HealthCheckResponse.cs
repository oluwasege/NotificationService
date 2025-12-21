using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotificationService.Application.DTOs
{
    public sealed record HealthCheckResponse
    {
        public string Status { get; init; } = "Healthy";
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public Dictionary<string, ProviderHealthResponse> Providers { get; init; } = [];
        public QueueHealthResponse Queue { get; init; } = new();
        public DatabaseHealthResponse Database { get; init; } = new();
    }

    public sealed record ProviderHealthResponse
    {
        public string Name { get; init; } = string.Empty;
        public bool IsAvailable { get; init; }
        public string Type { get; init; } = string.Empty;
    }

    public sealed record QueueHealthResponse
    {
        public int PendingCount { get; init; }
        public int TotalDepth { get; init; }
    }

    public sealed record DatabaseHealthResponse
    {
        public bool IsConnected { get; init; }
        public int PendingNotifications { get; init; }
        public int UnprocessedOutbox { get; init; }
    }

    public sealed record StatisticsResponse
    {
        public DateTime GeneratedAt { get; init; } = DateTime.Now;
        public Dictionary<string, int> NotificationsByStatus { get; init; } = [];
        public Dictionary<string, int> NotificationsByType { get; init; } = [];
        public int TotalToday { get; init; }
        public int DeliveredToday { get; init; }
        public int FailedToday { get; init; }
        public double SuccessRate { get; init; }
    }
}
