using telegram_bot_aca.Bot.Commands;
using telegram_bot_aca.Services;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace telegram_bot_aca.Bot;

public class TelegramUpdateHandler : ITelegramUpdateHandler
{
    private readonly IServiceScopeFactory _scopeFactory;

    public TelegramUpdateHandler(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task HandleUpdateAsync(Update update, CancellationToken cancellationToken)
    {
        var chatId = update.Message?.Chat.Id ?? update.CallbackQuery?.Message?.Chat.Id;
        if (!chatId.HasValue)
        {
            return;
        }

        var chatType = update.Message?.Chat.Type ?? update.CallbackQuery?.Message?.Chat.Type;

        if (chatType!=ChatType.Private)
        {
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        var commandFactory = scope.ServiceProvider.GetRequiredService<ITelegramCommandFactory>();
        var sessionStore = scope.ServiceProvider.GetRequiredService<IConversionSessionStore>();

        var context = new TelegramCommandContext
        {
            Update = update,
            ChatId = chatId.Value,
            MessageId = update.Message?.MessageId ?? update.CallbackQuery?.Message?.MessageId,
            Text = update.Message?.Text,
            CallbackData = update.CallbackQuery?.Data,
            IsCallbackQuery = update.CallbackQuery != null
        };

        context.User = await userService.GetByTelegramChatIdAsync(chatId.Value, cancellationToken);

        if (sessionStore.TryGet(context.ChatId, out var pending))
        {
            context.PendingConversionSession = pending;
        }
        
        var command = commandFactory.Resolve(context);
        await command.HandleAsync(context, cancellationToken);
    }
}