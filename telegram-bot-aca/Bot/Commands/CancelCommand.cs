using telegram_bot_aca.Services;
using Telegram.Bot;

namespace telegram_bot_aca.Bot.Commands;

public class CancelCommand:ITelegramCommand
{
    private readonly ITelegramBotClient _botClient;
    private readonly IConversionSessionStore _sessionStore;
    private readonly IJobCancellationService _jobCancellationService;

    public CancelCommand(ITelegramBotClient botClient,IConversionSessionStore sessionStore, IJobCancellationService jobCancellationService)
    {
        _botClient = botClient;
        _sessionStore = sessionStore;
        _jobCancellationService = jobCancellationService;
    }
    
    public bool CanHandle(TelegramCommandContext context)
    {
        if (context.IsCallbackQuery)
        {
            return string.Equals(context.CallbackData, "cancel", StringComparison.OrdinalIgnoreCase);
        }

        return !string.IsNullOrWhiteSpace(context.Text) &&
               context.Text.StartsWith("/cancel", StringComparison.OrdinalIgnoreCase);
    }

    public async Task HandleAsync(TelegramCommandContext context, CancellationToken cancellationToken)
    {
        if (!context.IsCallbackQuery && !string.IsNullOrWhiteSpace(context.Text))
        {
            var split = context.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (split.Length != 2)
            {
                await _botClient.SendMessage(context.ChatId, "Please provide job id in this format /cancel <jobId>",
                    cancellationToken: cancellationToken);
                return;
            }

            if (context.User is null)
            {
                await _botClient.SendMessage(context.ChatId, "You are not registered!",
                    cancellationToken: cancellationToken);
                return;
            }

            if (!Guid.TryParse(split[1], out var jobId))
            {
                await _botClient.SendMessage(context.ChatId, "Invalid job id!", cancellationToken: cancellationToken);
                return;
            }

            var outcome = await _jobCancellationService.CancelAsync(context.User.Id, jobId, cancellationToken);
            var message = outcome switch
            {
                JobCancelOutcome.NotFound => "Job not found.",
                JobCancelOutcome.AlreadyFinalized => "Job is already finalized and cannot be cancelled.",
                JobCancelOutcome.CancelledQueued => "Queued job cancelled successfully.",
                JobCancelOutcome.CancellationRequested =>
                    "Cancellation requested for processing job. It will stop shortly.",
                _ => "Cancellation request processed."
            };
            await _botClient.SendMessage(context.ChatId, message, cancellationToken: cancellationToken);
            return;
        }

        _sessionStore.Remove(context.ChatId);
        if (context.IsCallbackQuery && context.Update.CallbackQuery is not null)
        {
            await _botClient.AnswerCallbackQuery(context.Update.CallbackQuery.Id, "Cancelled",
                cancellationToken: cancellationToken);
            if (context.MessageId.HasValue)
            {
                await _botClient.EditMessageText(context.ChatId, context.MessageId.Value,
                    "Pending conversion cancelled.", cancellationToken: cancellationToken);
                return;
            }
        }

        await _botClient.SendMessage(context.ChatId, "Pending conversion cancelled.",
            cancellationToken: cancellationToken);
    }
}