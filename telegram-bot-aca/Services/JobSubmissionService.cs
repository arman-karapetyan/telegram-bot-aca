using Microsoft.EntityFrameworkCore;
using telegram_bot_aca.Data;
using telegram_bot_aca.Data.Entites;
using telegram_bot_aca.Models;
using telegram_bot_aca.Utilities;

namespace telegram_bot_aca.Services;

public class JobSubmissionService : IJobSubmissionService
{
    private readonly AppDbContext _dbContext;
    private readonly IJobQueue _jobQueue;

    public JobSubmissionService(AppDbContext dbContext,IJobQueue jobQueue)
    {
        _dbContext = dbContext;
        _jobQueue = jobQueue;
    }

    public async Task<Guid> SubmitAsync(JobSubmissionRequest request, CancellationToken cancellationToken)
    {
        var userExists = await _dbContext.Users.AnyAsync(x => x.Id == request.UserId, cancellationToken);
        if (!userExists)
        {
            throw new InvalidOperationException("User not found");
        }

        var job = new ProcessingJob
        {
            UserId = request.UserId,
            AssetType = request.AssetType,
            SourcePath = request.SourcePath,
            TargetFormat = string.IsNullOrWhiteSpace(request.TargetFormat)
                ? ConversionDefaults.DefaultTargetFormat(request.AssetType)
                : request.TargetFormat.Trim().ToLowerInvariant(),
            AudioMode = string.IsNullOrWhiteSpace(request.TargetFormat)
                ? ConversionDefaults.DefaultAudioMode(request.AssetType)
                : request.AudioMode?.Trim().ToLowerInvariant(),
            Status = JobStatus.Queued
        };

        var queueEntry = new QueueStateEntry
        {
            Id = job.Id,
            Status = job.Status,
        };
        
        await _dbContext.Jobs.AddAsync(job, cancellationToken);
        await _dbContext.QueueStates.AddAsync(queueEntry, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _jobQueue.EnqueueAsync(new QueueJobItem(job.Id), cancellationToken);
        return job.Id;
    }

    public async Task<JobStatusDto?> GetJobStatusAsync(Guid userId, Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _dbContext.Jobs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == jobId && x.UserId == userId, cancellationToken);

        if (job is null)
        {
            return null;
        }

        var queuePosition = job.Status == JobStatus.Queued
            ? await _dbContext.Jobs.CountAsync(x =>
                x.Status == JobStatus.Queued && x.CreatedAt <= job.CreatedAt && x.Id != jobId, cancellationToken) + 1
            : 0;

        return new JobStatusDto
        {
            JobId = job.Id,
            Status = job.Status,
            QueuePosition = queuePosition,
            ResultPath = job.ResultPath,
            ErrorMessage = job.ErrorMessage
        };
    }
}