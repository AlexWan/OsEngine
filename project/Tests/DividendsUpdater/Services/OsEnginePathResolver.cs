using System.Reflection;

namespace DividendsUpdater.Services;

public static class OsEnginePathResolver
{
    public static string Resolve(string? settingsPath)
    {
        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

        string localOsEngine = Path.Combine(baseDirectory, "OsEngine.exe");
        if (File.Exists(localOsEngine))
        {
            return localOsEngine;
        }

        if (!string.IsNullOrWhiteSpace(settingsPath))
        {
            string fullSettingsPath = Path.GetFullPath(settingsPath);
            if (File.Exists(fullSettingsPath))
            {
                return fullSettingsPath;
            }
        }

        string defaultPath = Path.GetFullPath(Path.Combine(
            baseDirectory,
            "..", "..", "..", "..", "..",
            "OsEngine", "bin", "Debug", "OsEngine.exe"));

        if (File.Exists(defaultPath))
        {
            return defaultPath;
        }

        string? foundPath = FindOsEngineUpwards(baseDirectory);

        if (!string.IsNullOrEmpty(foundPath))
        {
            return foundPath;
        }

        return string.Empty;
    }

    private static string? FindOsEngineUpwards(string startDirectory)
    {
        try
        {
            DirectoryInfo? directory = new DirectoryInfo(startDirectory);

            for (int i = 0; i < 6 && directory != null; i++)
            {
                string candidate = Path.Combine(directory.FullName, "OsEngine", "bin", "Debug", "OsEngine.exe");

                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }
}
