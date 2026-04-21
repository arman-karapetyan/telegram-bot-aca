namespace telegram_bot_aca.Data.Entites;

public class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; }
    public long? TelegramChatId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ProcessingJob> Jobs { get; set; } = new List<ProcessingJob>();
}