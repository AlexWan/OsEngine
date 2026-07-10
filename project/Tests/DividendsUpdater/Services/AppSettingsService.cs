using System.Text.Json;
using DividendsUpdater.Models;

namespace DividendsUpdater.Services;

public static class AppSettingsService
{
    private const string SettingsFileName = "app-settings.json";

    public static AppSettings LoadOrCreate()
    {
        string filePath = GetSettingsPath();

        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(json);

            if (settings != null)
            {
                return settings;
            }
        }

        var defaultSettings = new AppSettings
        {
            OsEnginePath = GetDefaultOsEnginePath()
        };

        Save(defaultSettings);
        return defaultSettings;
    }

    public static void Save(AppSettings settings)
    {
        string filePath = GetSettingsPath();
        string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(filePath, json);
    }

    private static string GetSettingsPath()
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFileName);
    }

    private static string GetDefaultOsEnginePath()
    {
        return OsEnginePathResolver.Resolve(string.Empty);
    }
}
