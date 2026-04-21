using Microsoft.Extensions.Options;
using telegram_bot_aca.Bot;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace telegram_bot_aca;

public sealed class TelegramPollingService : BackgroundService
{
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<TelegramBotInitializer> _logger;
    private readonly ITelegramUpdateHandler _updateHandler;
    private readonly TelegramBotOptions _botOptions;

    public TelegramPollingService(ITelegramBotClient botClient, ILogger<TelegramBotInitializer> logger,
        IOptions<TelegramBotOptions> options, ITelegramUpdateHandler updateHandler)
    {
        _botClient = botClient;
        _logger = logger;
        _updateHandler = updateHandler;
        _botOptions = options.Value;
    }


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_botOptions.CommunicationMode != BotCommunicationMode.Polling)
        {
            _logger.Log(LogLevel.Information,
                $"Polling worker is disabled because mode is {_botOptions.CommunicationMode}");
            return;
        }

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery, ],
            DropPendingUpdates = false
        };

        _botClient.StartReceiving(updateHandler: OnUpdateAsync, errorHandler: OnErrorAsync,
            receiverOptions: receiverOptions, cancellationToken: stoppingToken);
    }

    private async Task OnUpdateAsync(ITelegramBotClient _, Update update, CancellationToken cancellationToken)
    {
        await _updateHandler.HandleUpdateAsync(update, cancellationToken);
    }

    private Task OnErrorAsync(ITelegramBotClient _, Exception exception, HandleErrorSource source,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, $"Telegram polling error from {source}");
        return Task.CompletedTask;
    }
}