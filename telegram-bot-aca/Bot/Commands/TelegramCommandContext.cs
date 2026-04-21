using telegram_bot_aca.Data.Entites;
using Telegram.Bot.Types;

namespace telegram_bot_aca.Bot.Commands;

public class TelegramCommandContext
{
    public Update Update { get; set; }
    public long ChatId { get; set; }
    public int? MessageId { get; set; }
    public string? Text { get; set; }
    public string? CallbackData { get; set; }
    public bool IsCallbackQuery { get; set; }
    public AppUser? User { get; set; }
    public PendingConversionSession? PendingConversionSession { get; set; }
}

public class PendingConversionSession
{
    public JobAssetType AssetType { get; set; }
    public string SourcePath { get; set; }
    public bool HasAudio { get; set; }
    public string? SelectedVideoFormat { get; set; }
}