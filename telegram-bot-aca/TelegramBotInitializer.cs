using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace telegram_bot_aca;

public sealed class TelegramBotInitializer:IHostedService
{
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<TelegramBotInitializer> _logger;
    private readonly TelegramBotOptions _botOptions;

    public TelegramBotInitializer(ITelegramBotClient botClient, ILogger<TelegramBotInitializer> logger, IOptions<TelegramBotOptions> options)
    {
        _botClient = botClient;
        _logger = logger;
        _botOptions = options.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var me = await _botClient.GetMe(cancellationToken);
        _logger.Log(LogLevel.Information, "Bot started: {Username}", me.Username);

        if (_botOptions.CommunicationMode==BotCommunicationMode.Webhook)
        {
            //SET BOT WEB HOOK
            _logger.Log(LogLevel.Information, "Setting webhook");
            return;
        }

        await _botClient.DeleteWebhook(cancellationToken: cancellationToken);
        _logger.Log(LogLevel.Information, "Deleted webhook for long polling mode");

        var commands = new BotCommand[]
        {
            new BotCommand("start", "Start the bot"),
        };

        await _botClient.SetMyCommands(commands, cancellationToken: cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}