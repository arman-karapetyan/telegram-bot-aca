using telegram_bot_aca.Bot.Commands;

namespace telegram_bot_aca.Services;

public interface IConversionSessionStore
{
    void Set(long chatId, PendingConversionSession session);
    bool TryGet(long chatId, out PendingConversionSession session);
    void Remove(long chatId);
}