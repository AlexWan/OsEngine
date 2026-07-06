using DividendsUpdater.Models;

namespace DividendsUpdater.Services;

public class DividendUpdater
{
    private readonly AppSettings _settings;

    public DividendUpdater(AppSettings settings)
    {
        _settings = settings;
    }

    public void Run(IReadOnlyList<string>? tickers = null)
    {
        Console.WriteLine("[Updater] Starting dividends update...");
        Console.WriteLine($"[Updater] OsEngine path: {_settings.OsEnginePath}");

        WikiDividendsWriter.EnsureDividendsFolder(_settings.OsEnginePath);

        List<WikiSecurity> stocks = WikiSecuritiesReader.ReadRussianStocks(_settings.OsEnginePath);

        if (tickers != null && tickers.Count > 0)
        {
            stocks = stocks.Where(s => tickers.Contains(s.Name, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        Console.WriteLine($"[Updater] Found {stocks.Count} stocks to process");

        int successCount = 0;
        int failCount = 0;

        using var parser = new SmartLabParser(_settings.RequestDelayMs);

        foreach (WikiSecurity stock in stocks)
        {
            try
            {
                List<WikiDividend> dividends = parser.ParseDividends(stock.Name);

                if (dividends.Count == 0)
                {
                    Console.WriteLine($"[Updater] No dividends found for {stock.Name}");
                    failCount++;
                    continue;
                }

                WikiDividendsWriter.SaveDividends(_settings.OsEnginePath, stock.Name, dividends);
                successCount++;
            }
            catch (Exception error)
            {
                Console.WriteLine($"[Updater] Failed to process {stock.Name}: {error.Message}");
                failCount++;
            }
        }

        Console.WriteLine($"[Updater] Completed. Success: {successCount}, Failed: {failCount}");
    }
}
