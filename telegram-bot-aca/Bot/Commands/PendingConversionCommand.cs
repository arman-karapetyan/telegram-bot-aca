using telegram_bot_aca.Data.Entites;
using telegram_bot_aca.Models;
using telegram_bot_aca.Services;
using Telegram.Bot;

namespace telegram_bot_aca.Bot.Commands;

public class PendingConversionCommand : ITelegramCommand
{
    private readonly ITelegramBotClient _botClient;
    private readonly IConversionSessionStore _sessionStore;
    private readonly IJobSubmissionService _jobSubmissionService;

    public PendingConversionCommand(ITelegramBotClient botClient, IConversionSessionStore sessionStore,IJobSubmissionService jobSubmissionService)
    {
        _botClient = botClient;
        _sessionStore = sessionStore;
        _jobSubmissionService = jobSubmissionService;
    }

    public bool CanHandle(TelegramCommandContext context)
    {
        return context.User is not null &&
               context.PendingConversionSession is not null &&
               (!string.IsNullOrWhiteSpace(context.Text) || !string.IsNullOrWhiteSpace(context.CallbackData));
    }

    public async Task HandleAsync(TelegramCommandContext context, CancellationToken cancellationToken)
    {
        var pending = context.PendingConversionSession!;
        var selection = GetSelection(context);
        if (selection is null)
        {
            return;
        }

        switch (pending.AssetType)
        {
            case JobAssetType.Image:
                break;
            case JobAssetType.Video:
                await HandleVideoSelectionAsync(context, pending, selection, cancellationToken);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private async Task HandleVideoSelectionAsync(TelegramCommandContext context, PendingConversionSession pending,
        string selection, CancellationToken cancellationToken)
    {
        if (pending.SelectedVideoFormat is null)
        {
            if (!ConversionOptionCatalog.VideoFormats.Contains(selection))
            {
                await ReplayInvalidAsync(context,
                    $"Invalid video format. Choose from buttons or type: {string.Join(',', ConversionOptionCatalog.VideoFormats)}",
                    cancellationToken);
                return;
            }

            pending.SelectedVideoFormat = selection;
            _sessionStore.Set(context.ChatId, pending);
            if (!pending.HasAudio)
            {
                await QueueVideoJobAsync(context, pending, "mute", cancellationToken);
                return;
            }

            var prompt = "Audio detected. Choose audio format: ";
            if (context.IsCallbackQuery && context.MessageId.HasValue)
            {
                await _botClient.EditMessageText(
                    context.ChatId,
                    context.MessageId.Value,
                    prompt,
                    replyMarkup: ConversionOptionCatalog.BuildAudiModeKeyboard(),
                    cancellationToken: cancellationToken);
            }
            else
            {
                await _botClient.SendMessage(
                    context.ChatId,
                    prompt,
                    replyMarkup: ConversionOptionCatalog.BuildAudiModeKeyboard(),
                    cancellationToken: cancellationToken);
            }

            return;
        }

        if (!ConversionOptionCatalog.AudioModes.Contains(selection))
        {
            await ReplayInvalidAsync(context,
                "Invalid audio mode. Choose from buttons or type: " +
                string.Join(',', ConversionOptionCatalog.AudioModes), cancellationToken);
            return;
        }

        await ReplyOrEditAsync(context, "Video job queued, please wait...", cancellationToken);
        await QueueVideoJobAsync(context, pending, selection, cancellationToken);
    }

    private async Task QueueVideoJobAsync(TelegramCommandContext context, PendingConversionSession pending, string audioMode,
        CancellationToken cancellationToken)
    {
        var jobId = await _jobSubmissionService.SubmitAsync(new JobSubmissionRequest
        {
            UserId = context.User.Id,
            AssetType = JobAssetType.Video,
            SourcePath = pending.SourcePath,
            TargetFormat = pending.SelectedVideoFormat,
            AudioMode = audioMode
        }, cancellationToken);
        _sessionStore.Remove(context.ChatId);

        var status = await _jobSubmissionService.GetJobStatusAsync(context.User.Id, jobId, cancellationToken);
        var text = $"Video job queued.\nJob Id: {jobId}\nQueue Position: #{status?.QueuePosition ?? 1}";
        await ReplyOrEditAsync(context, text, cancellationToken);
    }

    private string? GetSelection(TelegramCommandContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.CallbackData))
        {
            var parts = context.CallbackData.Split(':',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length == 2 ? parts[1].ToLowerInvariant() : null;
        }

        return string.IsNullOrWhiteSpace(context.Text) ? null : context.Text.ToLowerInvariant();
    }

    private async Task ReplayInvalidAsync(TelegramCommandContext context, string text,
        CancellationToken cancellationToken)
    {
        if (context.IsCallbackQuery && context.Update.CallbackQuery is not null)
        {
            await _botClient.AnswerCallbackQuery(context.Update.CallbackQuery.Id, text, showAlert: true,
                cancellationToken: cancellationToken);
            return;
        }

        await _botClient.SendMessage(context.ChatId, text, cancellationToken: cancellationToken);
    }

    private async Task ReplyOrEditAsync(TelegramCommandContext context, string text,
        CancellationToken cancellationToken)
    {
        if ( context.IsCallbackQuery && context.Update.CallbackQuery is not null)
        {
            await _botClient.AnswerCallbackQuery(context.Update.CallbackQuery.Id, "Queued",
                cancellationToken: cancellationToken);
            if (context.MessageId.HasValue)
            {
                await _botClient.EditMessageText(context.ChatId, context.MessageId.Value, text,
                    cancellationToken: cancellationToken);
                return;
            }
        }

        await _botClient.SendMessage(context.ChatId, text, cancellationToken: cancellationToken);
    }
}