namespace telegram_bot_aca.Services;

public interface IJobCancellationService
{
    Task<JobCancelOutcome> CancelAsync(Guid userId, Guid jobId, CancellationToken cancellationToken);
}