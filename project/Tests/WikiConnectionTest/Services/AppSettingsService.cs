/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple.pdf
*/

using System.Text.Json;
using WikiConnectionTest.Models;

namespace WikiConnectionTest.Services
{
    /// <summary>
    /// Loads and saves application settings.
    /// </summary>
    public static class AppSettingsService
    {
        private const string FileName = "app-settings.json";

        public static AppSettings Load(string baseDirectory)
        {
            string filePath = Path.Combine(baseDirectory, FileName);

            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(json);

                    if (settings != null)
                    {
                        Console.WriteLine($"[Settings] Loaded from {filePath}");
                        return Normalize(settings, baseDirectory);
                    }
                }
                catch (Exception error)
                {
                    Console.WriteLine($"[Settings] Failed to load {filePath}: {error.Message}");
                }
            }

            Console.WriteLine("[Settings] Configuration not found. Please configure the application.");

            AppSettings newSettings = Prompt(baseDirectory);
            Save(filePath, newSettings);

            Console.WriteLine($"[Settings] Saved to {filePath}");
            Console.WriteLine("[Settings] This file is ignored by git. Do not commit it.");

            return newSettings;
        }

        public static void Save(string filePath, AppSettings settings)
        {
            string? directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(filePath, json);
        }

        private static AppSettings Prompt(string baseDirectory)
        {
            string defaultPath = Path.GetFullPath(Path.Combine(
                baseDirectory,
                "..", "..", "..", "..", "..", "..",
                "OsEngine", "bin", "Debug", "OsEngine.exe"));

            Console.Write($"Path to OsEngine.exe [{defaultPath}]: ");
            string? input = Console.ReadLine()?.Trim();

            string osEnginePath = string.IsNullOrEmpty(input) ? defaultPath : input;
            osEnginePath = Path.GetFullPath(osEnginePath);

            Console.Write("MCP base URL [http://localhost:6500]: ");
            string? url = Console.ReadLine()?.Trim();

            Console.Write("MCP API key [osengine-mcp-default-key]: ");
            string? key = Console.ReadLine()?.Trim();

            return new AppSettings
            {
                OsEnginePath = osEnginePath,
                McpBaseUrl = string.IsNullOrEmpty(url) ? "http://localhost:6500" : url,
                McpApiKey = string.IsNullOrEmpty(key) ? "osengine-mcp-default-key" : key,
                SecurityLoadTimeoutSeconds = 300
            };
        }

        private static AppSettings Normalize(AppSettings settings, string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(settings.OsEnginePath))
            {
                settings.OsEnginePath = Path.GetFullPath(Path.Combine(
                    baseDirectory,
                    "..", "..", "..", "..", "..", "..",
                    "OsEngine", "bin", "Debug", "OsEngine.exe"));
            }
            else
            {
                settings.OsEnginePath = Path.GetFullPath(settings.OsEnginePath);
            }

            if (string.IsNullOrWhiteSpace(settings.McpBaseUrl))
            {
                settings.McpBaseUrl = "http://localhost:6500";
            }

            if (string.IsNullOrWhiteSpace(settings.McpApiKey))
            {
                settings.McpApiKey = "osengine-mcp-default-key";
            }

            if (settings.McpReadyTimeoutSeconds <= 0)
            {
                settings.McpReadyTimeoutSeconds = 60;
            }

            if (settings.SecurityLoadTimeoutSeconds <= 0)
            {
                settings.SecurityLoadTimeoutSeconds = 300;
            }

            return settings;
        }
    }
}
