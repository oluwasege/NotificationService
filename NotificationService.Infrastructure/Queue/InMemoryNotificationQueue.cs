using System.Threading.Channels;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Interfaces;

namespace NotificationService.Infrastructure.Queue;

public class InMemoryNotificationQueue : INotificationQueue
{
    private readonly Channel<Notification> _highPriorityChannel;
    private readonly Channel<Notification> _normalPriorityChannel;
    private readonly Channel<Notification> _lowPriorityChannel;

    public InMemoryNotificationQueue()
    {
        var options = new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        };

        _highPriorityChannel = Channel.CreateBounded<Notification>(options);
        _normalPriorityChannel = Channel.CreateBounded<Notification>(options);
        _lowPriorityChannel = Channel.CreateBounded<Notification>(options);
    }

    public async ValueTask EnqueueAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        var channel = notification.Priority switch
        {
            Domain.Enums.NotificationPriority.Critical or Domain.Enums.NotificationPriority.High => _highPriorityChannel,
            Domain.Enums.NotificationPriority.Normal => _normalPriorityChannel,
            _ => _lowPriorityChannel
        };

        await channel.Writer.WriteAsync(notification, cancellationToken);
    }

    public async ValueTask<Notification?> DequeueAsync(CancellationToken cancellationToken = default)
    {
        // Priority-based dequeue: high -> normal -> low
        if (_highPriorityChannel.Reader.TryRead(out var highPriorityNotification))
        {
            return highPriorityNotification;
        }

        if (_normalPriorityChannel.Reader.TryRead(out var normalPriorityNotification))
        {
            return normalPriorityNotification;
        }

        if (_lowPriorityChannel.Reader.TryRead(out var lowPriorityNotification))
        {
            return lowPriorityNotification;
        }

        // Wait for any notification
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        var highTask = _highPriorityChannel.Reader.WaitToReadAsync(cts.Token).AsTask();
        var normalTask = _normalPriorityChannel.Reader.WaitToReadAsync(cts.Token).AsTask();
        var lowTask = _lowPriorityChannel.Reader.WaitToReadAsync(cts.Token).AsTask();

        await Task.WhenAny(highTask, normalTask, lowTask);

        if (_highPriorityChannel.Reader.TryRead(out highPriorityNotification))
        {
            return highPriorityNotification;
        }

        if (_normalPriorityChannel.Reader.TryRead(out normalPriorityNotification))
        {
            return normalPriorityNotification;
        }

        if (_lowPriorityChannel.Reader.TryRead(out lowPriorityNotification))
        {
            return lowPriorityNotification;
        }

        return null;
    }

    public int GetQueueCount()
    {
        return _highPriorityChannel.Reader.Count +
               _normalPriorityChannel.Reader.Count +
               _lowPriorityChannel.Reader.Count;
    }
}
