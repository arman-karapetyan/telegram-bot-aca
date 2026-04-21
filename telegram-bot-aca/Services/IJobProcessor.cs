using telegram_bot_aca.Data.Entites;

namespace telegram_bot_aca.Services;

public interface IJobProcessor
{
    Task<string> ProcessAsync(ProcessingJob job, CancellationToken cancellationToken);
}