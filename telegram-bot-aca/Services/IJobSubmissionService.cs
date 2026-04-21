using telegram_bot_aca.Models;

namespace telegram_bot_aca.Services;

public interface IJobSubmissionService
{
    Task<Guid> SubmitAsync(JobSubmissionRequest request, CancellationToken cancellationToken);
    Task<JobStatusDto?> GetJobStatusAsync(Guid userId, Guid jobId, CancellationToken cancellationToken);
}