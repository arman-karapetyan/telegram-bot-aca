using System.Diagnostics;
using telegram_bot_aca.Data.Entites;
using telegram_bot_aca.Services;
using telegram_bot_aca.Utilities;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace telegram_bot_aca.Bot.Commands;

public class MediaUploadCommand : ITelegramCommand
{
    private readonly ITelegramBotClient _botClient;
    private readonly IConversionSessionStore _sessionStore;

    public MediaUploadCommand(ITelegramBotClient botClient, IConversionSessionStore sessionStore)
    {
        _botClient = botClient;
        _sessionStore = sessionStore;
    }

    public bool CanHandle(TelegramCommandContext context)
    {
        if (context.IsCallbackQuery || context.User == null)
        {
            return false;
        }

        var message = context.Update.Message;
        return message?.Video is not null || message?.Photo is { Length: > 0 } || message?.Document is not null;
    }


    public async Task HandleAsync(TelegramCommandContext context, CancellationToken cancellationToken)
    {
        try
        {
            var message = context.Update.Message!;
            DeleteFileIfExists(context.PendingConversionSession?.SourcePath);
            _sessionStore.Remove(context.ChatId);

            if (message.Video is not null)
            {
                EnsureWithinCloudLimit(message.Video.FileSize);
                await HandleVideoAsync(message.Video.FileId, context.ChatId, cancellationToken);
                return;
            }

            if (message.Photo is { Length: > 0 })
            {
            }

            if (message.Document is not null)
            {
                EnsureWithinCloudLimit(message.Document.FileSize);
                var assetInfo = DetermineAssetInfoFromDocument(message.Document);
                if (assetInfo is null)
                {
                    await _botClient.SendMessage(context.ChatId, "Unsupported file type.",
                        cancellationToken: cancellationToken);
                    return;
                }

                var info = assetInfo.Value;
                if (info.assetType==JobAssetType.Video)
                {
                    await HandleVideoAsync(message.Document.FileId, context.ChatId, cancellationToken);
                }
            }
        }
        catch (InvalidOperationException e)
        {
            await _botClient.SendMessage(context.ChatId, $"⚠️ {e.Message}", cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            await _botClient.SendMessage(context.ChatId, $"Exception: {e.Message}",
                cancellationToken: cancellationToken);
        }
    }

    private async Task HandleVideoAsync(string fileId, long chatId, CancellationToken cancellationToken)
    {
        var sourcePath = await DownloadTelegramFileAsync(fileId, ".mp4", cancellationToken);
        var hasAudio = await DetectVideoHasAudioAsync(sourcePath, ResolveFfprobePath(), cancellationToken);

        _sessionStore.Set(chatId, new PendingConversionSession
        {
            AssetType = JobAssetType.Video,
            SourcePath = sourcePath,
            HasAudio = hasAudio
        });

        await _botClient.SendMessage(
            chatId,
            "🎬 Video received. Please select conversion options via buttons.",
            replyMarkup: ConversionOptionCatalog.BuildVideoFormatKeyboard(),
            cancellationToken: cancellationToken);
    }

    private (JobAssetType assetType, string extension)? DetermineAssetInfoFromDocument(Document document)
    {
        var mime = (document.MimeType ?? string.Empty).ToLowerInvariant();
        var extension = Path.GetExtension(document.FileName ?? string.Empty).ToLowerInvariant();
        if (extension is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp" or ".ico")
        {
            return (JobAssetType.Image, extension);
        }

        if (extension is ".mp4" or ".mov" or ".avi" or ".mkv" or ".wmv" or ".flv" or ".webm" or ".mpg" or ".mpeg")
        {
            return (JobAssetType.Video, extension);
        }

        return null;
    }

    private async Task<bool> DetectVideoHasAudioAsync(string localPath, string ffprobePath,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffprobePath,
                Arguments = $"-v error -select_streams a -show_entries stream=codec_type -of csv=p=0 \"{localPath}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        try
        {
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output);
        }
        catch
        {
            return true;
        }
    }

    private async Task<string> DownloadTelegramFileAsync(string fileId, string fallbackExtension,
        CancellationToken cancellationToken)
    {
        var telegramFile = await _botClient.GetFile(fileId, cancellationToken);
        if (string.IsNullOrWhiteSpace(telegramFile.FilePath))
        {
            throw new InvalidOperationException("Telegram file path is null");
        }

        var extension = Path.GetExtension(telegramFile.FilePath ?? fallbackExtension);
        var incomingDirectory = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "incoming");
        Directory.CreateDirectory(incomingDirectory);
        var localPath = Path.Combine(incomingDirectory, $"{Guid.NewGuid()}{extension}");
        await using var stream = File.Create(localPath);
        await _botClient.DownloadFile(telegramFile.FilePath!, stream, cancellationToken);
        return localPath;
    }

    private void EnsureWithinCloudLimit(long? fileSizeBytes)
    {
        if (!fileSizeBytes.HasValue || fileSizeBytes.Value <= 0)
        {
            return;
        }

        const int maxCloudDownloadSizeMb = 20;
        var maxBytes = maxCloudDownloadSizeMb * 1024L * 1024L;
        if (fileSizeBytes.Value > maxBytes)
        {
            throw new InvalidOperationException(
                $"File is too large for Telegram cloud bot download ({fileSizeBytes.Value / (1024 * 1024)} MB). Current limit is {maxCloudDownloadSizeMb} MB");
        }
    }

    private static void DeleteFileIfExists(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            //ignore
        }
    }

    private string ResolveFfprobePath()
    {
        return MediaToolPathResolver.ResolveOrDefault("ff", "ffprobe");
    }
}