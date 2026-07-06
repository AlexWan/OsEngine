using System.Text;
using System.Text.Json;
using DividendsUpdater.Models;

namespace DividendsUpdater.Services;

public static class WikiSecuritiesReader
{
    public static List<WikiSecurity> ReadRussianStocks(string osEnginePath)
    {
        string? directory = Path.GetDirectoryName(osEnginePath);

        if (string.IsNullOrEmpty(directory))
        {
            throw new InvalidOperationException("Cannot determine OsEngine directory");
        }

        string filePath = Path.Combine(directory, "Wiki", "tinvest_securities.md");

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Wiki securities file not found: {filePath}");
        }

        string content = File.ReadAllText(filePath, Encoding.UTF8);
        string jsonl = ExtractCodeBlock(content, "jsonl");

        var stocks = new List<WikiSecurity>();

        foreach (string line in jsonl.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            WikiSecurity? security = JsonSerializer.Deserialize<WikiSecurity>(trimmed);

            if (security == null)
            {
                continue;
            }

            if (IsRussianStock(security))
            {
                stocks.Add(security);
            }
        }

        return stocks;
    }

    private static bool IsRussianStock(WikiSecurity security)
    {
        return string.Equals(security.SecurityType, "Stock", StringComparison.OrdinalIgnoreCase)
            && security.NameClass.Contains("rub", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractCodeBlock(string markdown, string language)
    {
        string startMarker = $"```{language}";
        int startIndex = markdown.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);

        if (startIndex < 0)
        {
            return string.Empty;
        }

        startIndex += startMarker.Length;
        int endIndex = markdown.IndexOf("```", startIndex, StringComparison.Ordinal);

        if (endIndex < 0)
        {
            return string.Empty;
        }

        return markdown[startIndex..endIndex].Trim();
    }
}
