using telegram_bot_aca.Data.Entites;

namespace telegram_bot_aca.Utilities;

public static class ConversionDefaults
{
    public static string DefaultTargetFormat(JobAssetType assetType) =>
        assetType == JobAssetType.Video ? "mp4" : "jpg";

    public static string? DefaultAudioMode(JobAssetType assetType) =>
        assetType == JobAssetType.Video ? "copy" : null;
}