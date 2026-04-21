namespace telegram_bot_aca.Data.Entites;

public class QueueStateEntry
{
    public Guid Id { get; set; }
    public JobStatus Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}