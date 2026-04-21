using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace telegram_bot_aca;

public sealed class TelegramBotInitializer : IHostedService
{
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<TelegramBotInitializer> _logger;
    private readonly TelegramBotOptions _botOptions;

    public TelegramBotInitializer(ITelegramBotClient botClient, ILogger<TelegramBotInitializer> logger,
        IOptions<TelegramBotOptions> options)
    {
        _botClient = botClient;
        _logger = logger;
        _botOptions = options.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var me = await _botClient.GetMe(cancellationToken);
        _logger.Log(LogLevel.Information, "Bot started: {Username}", me.Username);

        if (_botOptions.CommunicationMode == BotCommunicationMode.Webhook)
        {
            if (string.IsNullOrWhiteSpace(_botOptions.WebHookUrl))
            {
                _logger.LogWarning("Telegram mode is Webhook but WebHookUrl is not set.");
                return;
            }

            var url = BuildWebhookUrl(_botOptions.WebHookUrl, _botOptions.WebHookPath);
            await _botClient.SetWebhook(url: url, dropPendingUpdates: true, cancellationToken: cancellationToken);
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

    private string BuildWebhookUrl(string url, string path)
    {
        var normalizedPath = string.IsNullOrWhiteSpace(path) ? "telegram/webhook" : path.Trim();
        if (!normalizedPath.StartsWith('/'))
        {
            normalizedPath = $"/{normalizedPath}";
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return url.TrimEnd('/') + normalizedPath;
        }

        var uriAbsolutePath = uri.AbsolutePath;
        if (string.IsNullOrWhiteSpace(uriAbsolutePath) || uriAbsolutePath == "/")
        {
            return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/') + normalizedPath;
        }

        return url;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}