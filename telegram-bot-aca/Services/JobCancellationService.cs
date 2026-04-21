using Microsoft.EntityFrameworkCore;
using telegram_bot_aca.Data;
using telegram_bot_aca.Data.Entites;

namespace telegram_bot_aca.Services;

public class JobCancellationService : IJobCancellationService
{
    private readonly AppDbContext _dbContext;
    private readonly IJobExecutionCancellationRegistry _cancellationRegistry;

    public JobCancellationService(AppDbContext dbContext, IJobExecutionCancellationRegistry cancellationRegistry)
    {
        _dbContext = dbContext;
        _cancellationRegistry = cancellationRegistry;
    }

    public async Task<JobCancelOutcome> CancelAsync(Guid userId, Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _dbContext.Jobs.FirstOrDefaultAsync(x => x.Id == jobId && x.UserId == userId,
            cancellationToken);
        if (job is null)
        {
            return JobCancelOutcome.NotFound;
        }

        if (job.Status is JobStatus.Completed or JobStatus.Failed or JobStatus.Cancelled)
        {
            return JobCancelOutcome.AlreadyFinalized;
        }

        if (job.Status==JobStatus.Queued)
        {
            job.Status = JobStatus.Cancelled;
            job.ErrorMessage = "Cancelled by user.";
            job.CompletedAt = DateTime.UtcNow;
            await UpdateQueueStateAsync(_dbContext, job.Id, job.Status, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return JobCancelOutcome.CancelledQueued;
        }

        if (job.Status==JobStatus.Processing)
        {
            var requested = _cancellationRegistry.RequestCancel(jobId);
            return requested ? JobCancelOutcome.CancellationRequested : JobCancelOutcome.AlreadyFinalized;
        }

        return JobCancelOutcome.AlreadyFinalized;
    }
    
    private async Task UpdateQueueStateAsync(AppDbContext dbContext, Guid jobGuid, JobStatus status,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.QueueStates.FirstOrDefaultAsync(x => x.Id == jobGuid, cancellationToken);
        if (existing is null)
        {
            dbContext.QueueStates.Add(new QueueStateEntry
            {
                Id = jobGuid, Status = status,
            });
            return;
        }

        existing.Status = status;
        existing.UpdatedAt = DateTime.UtcNow;
    }
}