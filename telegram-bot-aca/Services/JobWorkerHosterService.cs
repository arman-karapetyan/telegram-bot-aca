using Microsoft.EntityFrameworkCore;
using telegram_bot_aca.Data;
using telegram_bot_aca.Data.Entites;

namespace telegram_bot_aca.Services;

public class JobWorkerHosterService : BackgroundService
{
    private const int WorkerCount = 1;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IJobQueue _jobQueue;
    private readonly ILogger<JobWorkerHosterService> _logger;
    private readonly List<Task> _workerTasks = [];

    public JobWorkerHosterService(IServiceScopeFactory scopeFactory, IJobQueue jobQueue,
        ILogger<JobWorkerHosterService> logger)
    {
        _scopeFactory = scopeFactory;
        _jobQueue = jobQueue;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var definedWorkers = Math.Max(1, WorkerCount);
        for (int i = 0; i < definedWorkers; i++)
        {
            var workerId = i + 1;
            _workerTasks.Add(Task.Run(() => RunWorkerLoopAsync(workerId, stoppingToken), stoppingToken));
        }

        return Task.WhenAll(_workerTasks);
    }

    private async Task RunWorkerLoopAsync(int workerId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Worker {WorkerId} started", workerId);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var item = await _jobQueue.DequeueAsync(cancellationToken);
                await ProcessJobAsync(workerId, item.JobId, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Worker {WorkerId} failed to process job", workerId);
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }

        _logger.LogInformation("Worker {WorkerId} stopped", workerId);
    }

    private async Task ProcessJobAsync(int workerId, Guid jobId, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var processor = scope.ServiceProvider.GetRequiredService<IJobProcessor>();
        var notifier = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var job = await dbContext
            .Jobs
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);

        if (job is null)
        {
            _logger.LogWarning("Worker {WorkerId} could not find job {JobId}", workerId, jobId);
            return;
        }

        if (job.Status != JobStatus.Queued)
        {
            _logger.LogInformation("Worker {WorkerId} job {JobId} is not queued, because status is {Status}", workerId,
                jobId, job.Status);
            return;
        }

        job.Status = JobStatus.Processing;
        job.StatedAt = DateTime.UtcNow;

        await UpdateQueueStateAsync(dbContext, jobId, job.Status, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (job.User.TelegramChatId.HasValue)
        {
            job.ProgressMessageId = await notifier.SendProgressMessageAsync(job.User.TelegramChatId.Value,
                "Processing started...", cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }


        try
        {
            var originalSizeBytes = GetFileSizeSafe(job.SourcePath);
            var sourceFormat = GetExtensionOrUnknown(job.SourcePath);
            var targetFormat = string.IsNullOrWhiteSpace(job.TargetFormat)
                ? "unknown"
                : job.TargetFormat.ToLowerInvariant();
            var audioMode = ResolveAudioMode(targetFormat, job.AudioMode);
            var resultPath = await processor.ProcessAsync(job, cancellationToken);
            var convertedSizeBytes = GetFileSizeSafe(resultPath);

            job.Status = JobStatus.Completed;
            job.ResultPath = resultPath;
            job.CompletedAt = DateTime.UtcNow;
            await UpdateQueueStateAsync(dbContext, jobId, job.Status, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            if (job.User.TelegramChatId.HasValue)
            {
                if (job.ProgressMessageId.HasValue)
                {
                    await notifier.UpdateProgressMessageAsync(job.User.TelegramChatId.Value,
                        job.ProgressMessageId.Value,
                        "Processing completed!",
                        cancellationToken);
                }

                var message =
                    $"Conversion completed successfully! Original file size: {originalSizeBytes} bytes, converted file size: {convertedSizeBytes} bytes, source format: {sourceFormat}, target format: {targetFormat}, audio mode: {audioMode}";
                var sent = await notifier.NotifyResultAsync(job.User.TelegramChatId.Value, resultPath, message,
                    cancellationToken);
            }
        }
        catch (Exception e)
        {
            job.Status = JobStatus.Failed;
            job.ErrorMessage = e.Message;
            job.CompletedAt = DateTime.UtcNow;
            await UpdateQueueStateAsync(dbContext, jobId, job.Status, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            if (job.User.TelegramChatId.HasValue)
            {
                if (job.ProgressMessageId.HasValue)
                {
                    await notifier.UpdateProgressMessageAsync(job.User.TelegramChatId.Value,
                        job.ProgressMessageId.Value,
                        "Processing failed!",
                        cancellationToken);
                }
                else
                {
                    
                }
            }
        }
    }

    private long GetFileSizeSafe(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return 0;
        }

        return new FileInfo(path).Length;
    }

    private string GetExtensionOrUnknown(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "unknown";
        }

        var extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        return string.IsNullOrWhiteSpace(extension) ? "unknown" : extension;
    }

    private string ResolveAudioMode(string targetFormat, string? requestedAudioMode)
    {
        var mode = string.IsNullOrWhiteSpace(requestedAudioMode)
            ? "copy"
            : requestedAudioMode.Trim().ToLowerInvariant();
        if (targetFormat == "gif")
        {
            return "mute";
        }

        return mode;
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