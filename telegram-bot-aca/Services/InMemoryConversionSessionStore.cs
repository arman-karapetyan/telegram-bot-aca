using System.Collections.Concurrent;
using telegram_bot_aca.Bot.Commands;

namespace telegram_bot_aca.Services;

public class InMemoryConversionSessionStore : IConversionSessionStore
{
    private readonly ConcurrentDictionary<long, PendingConversionSession> _sessions = new();

    public void Set(long chatId, PendingConversionSession session)
    {
        _sessions[chatId] = session;
    }

    public bool TryGet(long chatId, out PendingConversionSession session)
    {
        var found = _sessions.TryGetValue(chatId, out var value);
        session = value;
        return found;
    }

    public void Remove(long chatId)
    {
        _sessions.TryRemove(chatId, out _);
    }
}