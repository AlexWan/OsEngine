using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using DividendsUpdater.Models;

namespace DividendsUpdater.Services;

public class SmartLabParser : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly int _requestDelayMs;

    public SmartLabParser(int requestDelayMs)
    {
        _requestDelayMs = requestDelayMs;

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };

        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
    }

    public List<WikiDividend> ParseDividends(string ticker)
    {
        string url = $"https://smart-lab.ru/q/{ticker}/dividend/";
        string html = DownloadHtml(url);

        if (string.IsNullOrWhiteSpace(html))
        {
            return new List<WikiDividend>();
        }

        var dividends = new List<WikiDividend>();
        List<string> tables = ExtractTables(html, "financials dividends");
        DateTime today = DateTime.Today;

        foreach (string table in tables)
        {
            List<string> rows = ExtractRows(table);

            foreach (string row in rows)
            {
                List<string> cells = ExtractCells(row);

                if (cells.Count < 7)
                {
                    continue;
                }

                string rowTicker = StripHtml(cells[0]).Trim();

                if (!string.Equals(rowTicker, ticker, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // cells[1] = "дата Т-1" — last day to be in position for the dividend.
                string cutOffDateText = StripHtml(cells[1]).Trim();
                string periodText = StripHtml(cells[3]).Trim();
                string dividendText = StripHtml(cells[4]).Trim();
                string yieldText = StripHtml(cells[6]).Trim();

                if (!TryParseDate(cutOffDateText, out DateTime cutOffDate))
                {
                    continue;
                }

                int year = NumberParser.ExtractYear(periodText);

                if (year == 0)
                {
                    year = cutOffDate.Year;
                }

                decimal? dividendAmount = NumberParser.ToNullableDecimal(dividendText);
                decimal? dividendYield = NumberParser.ToNullableDecimal(yieldText);

                dividends.Add(new WikiDividend
                {
                    Year = year,
                    RegistryCloseDate = cutOffDate,
                    DividendAmount = dividendAmount,
                    DividendYield = dividendYield,
                    IsFuture = cutOffDate >= today
                });
            }
        }

        Thread.Sleep(_requestDelayMs);
        return MergeDuplicates(dividends);
    }

    private string DownloadHtml(string url)
    {
        try
        {
            using HttpResponseMessage response = _httpClient.GetAsync(url).GetAwaiter().GetResult();

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return string.Empty;
            }

            response.EnsureSuccessStatusCode();
            byte[] bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception error)
        {
            Console.WriteLine($"[SmartLab] Failed to download {url}: {error.Message}");
            return string.Empty;
        }
    }

    private static List<string> ExtractTables(string html, string className)
    {
        var tables = new List<string>();
        string pattern = $"<table[^>]*class=\"[^\"]*{Regex.Escape(className)}[^\"]*\"[^>]*>(.*?)</table>";
        MatchCollection matches = Regex.Matches(html, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                tables.Add(match.Groups[1].Value);
            }
        }

        return tables;
    }

    private static List<string> ExtractRows(string tableHtml)
    {
        var rows = new List<string>();
        MatchCollection matches = Regex.Matches(tableHtml, "<tr[^>]*>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                rows.Add(match.Groups[1].Value);
            }
        }

        return rows;
    }

    private static List<string> ExtractCells(string rowHtml)
    {
        var cells = new List<string>();
        MatchCollection matches = Regex.Matches(rowHtml, "<td[^>]*>(.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                cells.Add(match.Groups[1].Value);
            }
        }

        return cells;
    }

    private static string StripHtml(string html)
    {
        string withoutTags = Regex.Replace(html, "<[^>]+>", string.Empty);
        return WebUtility.HtmlDecode(withoutTags);
    }

    private static bool TryParseDate(string text, out DateTime date)
    {
        date = DateTime.MinValue;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string normalized = text.Trim();
        string[] formats = { "dd.MM.yyyy", "d.MM.yyyy", "dd.M.yyyy" };

        foreach (string format in formats)
        {
            if (DateTime.TryParseExact(normalized, format, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out date))
            {
                return true;
            }
        }

        return false;
    }

    private static List<WikiDividend> MergeDuplicates(List<WikiDividend> dividends)
    {
        return dividends
            .GroupBy(d => new { d.RegistryCloseDate, d.Year })
            .Select(g => g.First())
            .ToList();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
