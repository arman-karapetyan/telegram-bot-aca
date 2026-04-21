using Telegram.Bot;
using Telegram.Bot.Types;

namespace telegram_bot_aca.Services;

public class TelegramNotificationService : INotificationService
{
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<TelegramNotificationService> _logger;

    public TelegramNotificationService(ITelegramBotClient botClient,ILogger<TelegramNotificationService> logger)
    {
        _botClient = botClient;
        _logger = logger;
    }
    
    public async Task<int?> SendProgressMessageAsync(long chatId, string message, CancellationToken cancellationToken)
    {
        try
        {
            var send = await _botClient.SendMessage(chatId, message, cancellationToken: cancellationToken);
            return send.MessageId;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to send progress message to chat {ChatId}: {Message}", chatId, message);
            return null;
        }
    }

    public async Task UpdateProgressMessageAsync(long chatId, int messageId, string message, CancellationToken cancellationToken)
    {
        try
        {
            await _botClient.EditMessageText(chatId, messageId, message, cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to update progress message {MessageId} in chat {ChatId}: {Message}", messageId,
                chatId, message);
        }
    }

    public async Task<bool> NotifyResultAsync(long chatId, string filePath, string message,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(filePath);
            var fileName = Path.GetFileName(filePath);
            await _botClient.SendDocument(
                chatId: chatId,
                document: InputFile.FromStream(stream, fileName),
                caption: message,
                cancellationToken: cancellationToken);
            return true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to send result message to chat {ChatId}: {Message}", chatId, message);
            await _botClient.SendMessage(chatId, $"{message}\nResult file is ready but could not be delivered as attachment. ", cancellationToken: cancellationToken);
            return false;
        }
    }
}