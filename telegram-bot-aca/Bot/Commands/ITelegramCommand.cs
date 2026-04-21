namespace telegram_bot_aca.Bot.Commands;

public interface ITelegramCommand
{
    bool CanHandle(TelegramCommandContext context);
    Task HandleAsync(TelegramCommandContext context, CancellationToken cancellationToken);
}