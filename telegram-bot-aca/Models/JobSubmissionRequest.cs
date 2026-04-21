using telegram_bot_aca.Data.Entites;

namespace telegram_bot_aca.Models;

public class JobSubmissionRequest
{
    public Guid UserId { get; set; }
    public JobAssetType AssetType { get; set; }
    public string SourcePath { get; set; }
    public string TargetFormat { get; set; }
    public string? AudioMode { get; set; }
}