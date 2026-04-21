namespace telegram_bot_aca.Services;

public interface IJobExecutionCancellationRegistry
{
    void Register(Guid jobId, CancellationTokenSource cancellationTokenSource);
    void Unregister(Guid jobId);
    bool RequestCancel(Guid jobId);
}