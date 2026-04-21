using telegram_bot_aca.Data.Entites;

namespace telegram_bot_aca.Models;

public class JobStatusDto
{
    public Guid JobId { get; set; }
    public JobStatus Status { get; set; }
    public int QueuePosition { get; set; }
    public string? ResultPath { get; set; }
    public string? ErrorMessage { get; set; }
}