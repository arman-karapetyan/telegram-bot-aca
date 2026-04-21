namespace telegram_bot_aca.Utilities;

public static class MediaToolPathResolver
{
    public static string ResolveOrDefault(string configuredPath,string toolName)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var trimmed = configuredPath.Trim();
            if (LooksLikePath(trimmed) && !File.Exists(trimmed))
            {
                throw new InvalidOperationException($"Configured media tool path does not exist: {trimmed}");
            }

            return trimmed;
        }

        var candidate = GetCommonInstallPaths(toolName).FirstOrDefault(File.Exists);
        return candidate ?? toolName;
    }

    private static bool LooksLikePath(string value)
    {
        return Path.IsPathRooted(value) || value.Contains('/') || value.Contains('\\');
    }

    private static IEnumerable<string> GetCommonInstallPaths(string toolName)
    {
        yield return $"/opt/homebrew/bin/{toolName}";
        yield return $"/usr/local/bin/{toolName}";
        yield return $"/usr/bin/{toolName}";
    }
}