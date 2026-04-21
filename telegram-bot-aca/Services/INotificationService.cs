namespace telegram_bot_aca.Services;

public interface INotificationService
{
    Task<int?> SendProgressMessageAsync(long chatId, string message, CancellationToken cancellationToken);
    Task UpdateProgressMessageAsync(long chatId, int messageId, string message, CancellationToken cancellationToken);
    Task<bool> NotifyResultAsync(long chatId, string filePath, string message, CancellationToken cancellationToken);
}