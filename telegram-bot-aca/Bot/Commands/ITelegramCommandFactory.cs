namespace telegram_bot_aca.Bot.Commands;

public interface ITelegramCommandFactory
{
    ITelegramCommand Resolve(TelegramCommandContext context);
}