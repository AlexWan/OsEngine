using DividendsUpdater.Models;
using DividendsUpdater.Services;

namespace DividendsUpdater;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            AppSettings settings = AppSettingsService.LoadOrCreate();

            if (args.Contains("--interactive") || IsInteractiveRun())
            {
                settings = PromptSettings(settings);
                AppSettingsService.Save(settings);
            }

            string resolvedOsEnginePath = OsEnginePathResolver.Resolve(settings.OsEnginePath);

            if (string.IsNullOrWhiteSpace(resolvedOsEnginePath))
            {
                Console.WriteLine("[Error] OsEngine.exe not found. Cannot determine dividends output folder.");
                return 1;
            }

            settings.OsEnginePath = resolvedOsEnginePath;

            List<string>? tickers = null;
            int tickerIndex = Array.IndexOf(args, "--ticker");

            if (tickerIndex >= 0 && tickerIndex < args.Length - 1)
            {
                tickers = args[tickerIndex + 1]
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim())
                    .ToList();
            }

            var updater = new DividendUpdater(settings);
            updater.Run(tickers);

            if (IsInteractiveRun())
            {
                Console.WriteLine();
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey(true);
            }

            return 0;
        }
        catch (Exception error)
        {
            Console.WriteLine($"[Error] {error.Message}");
            Console.WriteLine(error.StackTrace);

            if (IsInteractiveRun())
            {
                Console.WriteLine();
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey(true);
            }

            return 1;
        }
    }

    private static bool IsInteractiveRun()
    {
        return Environment.UserInteractive && Console.In is StreamReader;
    }

    private static AppSettings PromptSettings(AppSettings current)
    {
        var settings = new AppSettings
        {
            OsEnginePath = current.OsEnginePath,
            RequestDelayMs = current.RequestDelayMs,
            HttpTimeoutSeconds = current.HttpTimeoutSeconds
        };

        Console.Write($"Path to OsEngine.exe [{settings.OsEnginePath}]: ");
        string? osEnginePath = Console.ReadLine();

        if (!string.IsNullOrWhiteSpace(osEnginePath))
        {
            settings.OsEnginePath = osEnginePath.Trim().Trim('"');
        }

        Console.Write($"Request delay ms [{settings.RequestDelayMs}]: ");
        string? delayInput = Console.ReadLine();

        if (int.TryParse(delayInput, out int delay))
        {
            settings.RequestDelayMs = delay;
        }

        return settings;
    }
}
