namespace telegram_bot_aca.Models;

public class QueueJobItem
{
    public readonly Guid JobId;

    public QueueJobItem(Guid jobId)
    {
        JobId = jobId;
    }
}