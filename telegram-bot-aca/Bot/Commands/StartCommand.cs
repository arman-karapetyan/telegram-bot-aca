using Telegram.Bot;

namespace telegram_bot_aca.Bot.Commands;

public class StartCommand : ITelegramCommand
{
    private readonly ITelegramBotClient _botClient;

    public StartCommand(ITelegramBotClient botClient)
    {
        _botClient = botClient;
    }

    public bool CanHandle(TelegramCommandContext context)
    {
        return !context.IsCallbackQuery && !string.IsNullOrWhiteSpace(context.Text) &&
               context.Text.StartsWith("/start", StringComparison.OrdinalIgnoreCase);
    }

    public async Task HandleAsync(TelegramCommandContext context, CancellationToken cancellationToken)
    {
        await _botClient.SendMessage(
            context.ChatId,
            """
            Welcome!
            User /register <username> to create an account.
            Send an image/video and choose conversion options via buttons.
            Use /status <jobId> and /cancle anytime. 
            """,
            cancellationToken: cancellationToken);
    }
}