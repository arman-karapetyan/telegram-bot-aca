namespace telegram_bot_aca.Data.Entites;

public class ProcessingJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public AppUser User { get; set; }
    public JobAssetType AssetType { get; set; }
    public string SourcePath { get; set; }
    public string TargetFormat { get; set; }
    public string? AudioMode { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Queued;
    public int? ProgressMessageId { get; set; }
    public string? ResultPath { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}