using Microsoft.EntityFrameworkCore;
using telegram_bot_aca.Data;
using telegram_bot_aca.Data.Entites;

namespace telegram_bot_aca.Services;

public class JobWorkerHosterService : BackgroundService
{
    private const int WorkerCount = 1;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IJobQueue _jobQueue;
    private readonly IJobExecutionCancellationRegistry _jobExecutionCancellationRegistry;
    private readonly ILogger<JobWorkerHosterService> _logger;
    private readonly List<Task> _workerTasks = [];

    public JobWorkerHosterService(IServiceScopeFactory scopeFactory, IJobQueue jobQueue,IJobExecutionCancellationRegistry jobExecutionCancellationRegistry,
        ILogger<JobWorkerHosterService> logger)
    {
        _scopeFactory = scopeFactory;
        _jobQueue = jobQueue;
        _jobExecutionCancellationRegistry = jobExecutionCancellationRegistry;
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
            using var jobCancellationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _jobExecutionCancellationRegistry.Register(jobId, jobCancellationCts);

            var originalSizeBytes = GetFileSizeSafe(job.SourcePath);
            var sourceFormat = GetExtensionOrUnknown(job.SourcePath);
            var targetFormat = string.IsNullOrWhiteSpace(job.TargetFormat)
                ? "unknown"
                : job.TargetFormat.ToLowerInvariant();
            var audioMode = ResolveAudioMode(targetFormat, job.AudioMode);
            var resultPath = await processor.ProcessAsync(job, jobCancellationCts.Token);
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

                var message = BuildCompletionMessage(sourceFormat, targetFormat, audioMode, originalSizeBytes,
                    convertedSizeBytes);
                var sent = await notifier.NotifyResultAsync(job.User.TelegramChatId.Value, resultPath, message,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            job.Status = JobStatus.Cancelled;
            job.ErrorMessage = "Cancelled by user";
            job.CompletedAt = DateTime.UtcNow;
            await UpdateQueueStateAsync(dbContext, jobId, job.Status, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            if (job.User.TelegramChatId.HasValue)
            {
                if (job.ProgressMessageId.HasValue)
                {
                    await notifier.UpdateProgressMessageAsync(job.User.TelegramChatId.Value,
                        job.ProgressMessageId.Value,
                        "Processing cancelled by user!",
                        cancellationToken);
                }
                else
                {
                    await notifier.NotifyAsync(job.User.TelegramChatId.Value,
                        "Processing cancelled by user!",
                        cancellationToken);
                }
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
                    await notifier.NotifyAsync(job.User.TelegramChatId.Value,
                        "Processing failed!",
                        cancellationToken);
                }
            }
        }
        finally
        {
            _jobExecutionCancellationRegistry.Unregister(jobId);
        }
    }

    private string BuildCompletionMessage(string sourceFormat, string targetFormat, string audioMode,
        long originalSizeBytes, long convertedSizeBytes)
    {
        var diffPercent = originalSizeBytes > 0
            ? ((double)(convertedSizeBytes - originalSizeBytes) / originalSizeBytes) * 100.0
            : 0;

        var direction = diffPercent <= 0 ? "smaller" : "larger";
        var absPercent = Math.Abs(diffPercent);

        return $"Processing completed successfully.\n" +
               $"Format: {sourceFormat} → {targetFormat}\n" +
               $"Audio: {audioMode}\n" +
               $"Size: {FormatBytes(originalSizeBytes)} → {FormatBytes(convertedSizeBytes)}\n" +
               $"Change: {absPercent:F1}% {direction}";
    }

    private string FormatBytes(long bytes)
    {
        if (bytes<=0)
        {
            return "0 B";
        }

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var size=(double)bytes;
        var unitIndex = 0;
        while (size>=1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:F2} {units[unitIndex]}";
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