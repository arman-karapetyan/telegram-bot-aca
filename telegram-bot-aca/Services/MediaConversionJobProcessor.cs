using System.Diagnostics;
using telegram_bot_aca.Data.Entites;
using telegram_bot_aca.Utilities;

namespace telegram_bot_aca.Services;

public class MediaConversionJobProcessor : IJobProcessor
{
    private readonly ILogger<MediaConversionJobProcessor> _logger;

    public MediaConversionJobProcessor(ILogger<MediaConversionJobProcessor> logger)
    {
        _logger = logger;
    }

    public async Task<string> ProcessAsync(ProcessingJob job, CancellationToken cancellationToken)
    {
        if (!File.Exists(job.SourcePath))
        {
            throw new FileNotFoundException($"Source file not found: {job.SourcePath}");
        }

        var processedDirectory = Path.Combine(Directory.GetCurrentDirectory(), "processed");
        Directory.CreateDirectory(processedDirectory);

        var targetFormat = string.IsNullOrWhiteSpace(job.TargetFormat)
            ? job.AssetType == JobAssetType.Video ? "mp4" : "jpg"
            : job.TargetFormat.Trim().ToLowerInvariant();

        return job.AssetType == JobAssetType.Image
            ? await ConvertImageAsync(job, targetFormat, processedDirectory, cancellationToken)
            : await ConvertVideoAsync(job, targetFormat, processedDirectory, cancellationToken);
    }

    private async Task<string> ConvertVideoAsync(ProcessingJob job, string targetFormat, string processedDirectory,
        CancellationToken cancellationToken)
    {
        var outputPath = Path.Combine(processedDirectory, $"{job.Id}.{targetFormat}");
        var videoArgs = BuildVideoCodecArgs(targetFormat);
        var audioArgs = BuildAudioCodecArgs(targetFormat, job.AudioMode);

        var args = $"-y -i \"{job.SourcePath}\" {videoArgs} {audioArgs} \"{outputPath}\"";
        var ffmpegPath = MediaToolPathResolver.ResolveOrDefault(string.Empty, "ffmpeg");
        _logger.LogInformation("Running video conversion for job {JobId}: {FfmpegPath} {Args}", job.Id, ffmpegPath,
            args);

        await RunProcessAsync(ffmpegPath, args, cancellationToken);
        return outputPath;
    }

    private async Task RunProcessAsync(string ffmpegPath, string args, CancellationToken cancellationToken)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = args,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            process.Start();
        }
        catch (Exception e)
        {
            throw new InvalidOperationException(
                $"Failed to start media tool '{ffmpegPath}'. Ensure ffmpeg/ffprobe is installed and set!");
        }

        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"FFmpeg process exited with code {process.ExitCode}.\nstderr: {stderr}\nstdout:{stdout}");
        }
    }

    private string BuildVideoCodecArgs(string targetFormat)
    {
        return targetFormat switch
        {
            "mp4" => "-c:v libx264 -pix_fmt yuv420p",
            "webm" => "-c:v libvpx-vp9 -pix_fmt yuv420p",
            "mov" => "-c:v libx264 -pix_fmt yuv420p",
            "avi" => "-c:v mpeg4",
            "gif" => "-vf fps=12,scale=640:-1:flags=lanczos -c:v gif",
            _ => throw new NotSupportedException($"Unsupported video format: {targetFormat}")
        };
    }

    private string BuildAudioCodecArgs(string targetFormat, string? audioMode)
    {
        var mode = string.IsNullOrWhiteSpace(audioMode) ? "copy" : audioMode.Trim().ToLowerInvariant();
        if (targetFormat == "gif")
        {
            return "-an";
        }

        if (targetFormat == "webm")
        {
            if (mode is "copy" or "aac" or "mp3")
            {
                _logger.LogWarning("Audio mode {AudioMode} is not compatible with webm format, Falling back to opus",
                    mode);
                mode = "opus";
            }
        }

        return mode switch
        {
            "copy" => "-c:a copy",
            "mute" => "-an",
            "aac" => "-c:a aac -b:a 192k",
            "mp3" => "-c:a libmp3lame -b:a 192k",
            "opus" => "-c:a libopus -b:a 128k",
            _ => "-c:a copy"
        };
    }


    private async Task<string> ConvertImageAsync(ProcessingJob job, string targetFormat, string processedDirectory,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}