using System.Collections.Concurrent;

namespace telegram_bot_aca.Services;

public class JobExecutionCancellationRegistry : IJobExecutionCancellationRegistry
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _registrations = new();
    
    public void Register(Guid jobId, CancellationTokenSource cancellationTokenSource)
    {
        _registrations[jobId] = cancellationTokenSource;
    }

    public void Unregister(Guid jobId)
    {
        _registrations.TryRemove(jobId, out _);
    }

    public bool RequestCancel(Guid jobId)
    {
        if (!_registrations.TryGetValue(jobId, out var cts))
        {
            return false;
        }

        cts.Cancel();
        return true;
    }
}