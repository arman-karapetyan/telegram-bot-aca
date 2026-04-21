using telegram_bot_aca.Services;
using Telegram.Bot;

namespace telegram_bot_aca.Bot.Commands;

public class RegisterCommand : ITelegramCommand
{
    private readonly ITelegramBotClient _botClient;
    private readonly IUserService _userService;

    public RegisterCommand(ITelegramBotClient botClient, IUserService userService)
    {
        _botClient = botClient;
        _userService = userService;
    }

    public bool CanHandle(TelegramCommandContext context)
    {
        return !context.IsCallbackQuery && !string.IsNullOrWhiteSpace(context.Text) &&
               context.Text.StartsWith("/register", StringComparison.OrdinalIgnoreCase);
    }

    public async Task HandleAsync(TelegramCommandContext context, CancellationToken cancellationToken)
    {
        var split = context.Text!.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (split.Length < 2)
        {
            await _botClient.SendMessage(context.ChatId, "Please provide username in this format /register <username>",
                cancellationToken: cancellationToken);
            return;
        }

        try
        {
            var user = await _userService.RegisterTelegramUserAsync(context.ChatId, split[1], cancellationToken);
            await _botClient.SendMessage(context.ChatId, $"Welcome {user.Username}!",
                cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException e)
        {
            await _botClient.SendMessage(context.ChatId, e.Message, cancellationToken: cancellationToken);
        }
    }
}