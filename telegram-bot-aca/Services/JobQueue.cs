using System.Threading.Channels;
using telegram_bot_aca.Models;

namespace telegram_bot_aca.Services;

public class JobQueue : IJobQueue
{
    private readonly Channel<QueueJobItem> _channel = Channel.CreateUnbounded<QueueJobItem>(new UnboundedChannelOptions
    {
        SingleReader = false,
        SingleWriter = false,
        AllowSynchronousContinuations = false
    });

    public ValueTask EnqueueAsync(QueueJobItem item, CancellationToken cancellationToken)
    {
        return _channel.Writer.WriteAsync(item, cancellationToken);
    }

    public ValueTask<QueueJobItem> DequeueAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }
}