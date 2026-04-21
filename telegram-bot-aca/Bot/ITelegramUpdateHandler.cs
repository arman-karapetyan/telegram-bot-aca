using Telegram.Bot.Types;

namespace telegram_bot_aca.Bot;

public interface ITelegramUpdateHandler
{
    Task HandleUpdateAsync(Update update, CancellationToken cancellationToken);
}