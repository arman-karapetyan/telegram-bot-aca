using Telegram.Bot.Types.ReplyMarkups;

namespace telegram_bot_aca.Bot;

public static class ConversionOptionCatalog
{
    public static readonly string[] ImageFormats = ["webp", "ico", "png", "jpg", "jpeg", "bmp"];
    public static readonly string[] VideoFormats = ["mp4", "webm", "mov", "avi", "gif"];
    public static readonly string[] AudioModes = ["copy", "mute", "aac", "mp3", "opus"];

    public static InlineKeyboardMarkup BuildImageFormatKeyboard()
    {
        return BuildKeyboard(ImageFormats, "imgfmt");
    }

    public static InlineKeyboardMarkup BuildVideoFormatKeyboard()
    {
        return BuildKeyboard(VideoFormats, "vidfmt");
    }

    public static InlineKeyboardMarkup BuildAudiModeKeyboard()
    {
        return BuildKeyboard(AudioModes, "aud");
    }

    private static InlineKeyboardMarkup BuildKeyboard(IEnumerable<string> values, string prefix)
    {
        var rows = values
            .Chunk(3)
            .Select(chunk => chunk
                .Select(value => InlineKeyboardButton.WithCallbackData(value.ToUpperInvariant(), $"{prefix}:{value}"))
                .ToArray())
            .ToList();
        rows.Add([InlineKeyboardButton.WithCallbackData("Cancel", "cancel")]);
        return new InlineKeyboardMarkup(rows);
    }
}