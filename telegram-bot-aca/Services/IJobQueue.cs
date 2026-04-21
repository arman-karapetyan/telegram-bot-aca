using telegram_bot_aca.Models;

namespace telegram_bot_aca.Services;

public interface IJobQueue
{
    ValueTask EnqueueAsync(QueueJobItem item, CancellationToken cancellationToken);
    ValueTask<QueueJobItem> DequeueAsync(CancellationToken cancellationToken);
}