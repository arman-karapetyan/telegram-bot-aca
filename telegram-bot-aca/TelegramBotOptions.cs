namespace telegram_bot_aca;

public class TelegramBotOptions
{
    public const string SectionName = "TelegramBot";

    public string Token { get; set; }
    public BotCommunicationMode CommunicationMode { get; set; } = BotCommunicationMode.Polling;
}

public enum BotCommunicationMode
{
    Polling,
    Webhook
}