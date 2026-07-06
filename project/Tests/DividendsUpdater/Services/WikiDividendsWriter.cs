using System.Globalization;
using System.Text;
using DividendsUpdater.Models;

namespace DividendsUpdater.Services;

public static class WikiDividendsWriter
{
    public static void EnsureDividendsFolder(string osEnginePath)
    {
        string? directory = Path.GetDirectoryName(osEnginePath);

        if (string.IsNullOrEmpty(directory))
        {
            throw new InvalidOperationException("Cannot determine OsEngine directory");
        }

        string dividendsFolder = Path.Combine(directory, "Wiki", "Dividends");

        if (!Directory.Exists(dividendsFolder))
        {
            Directory.CreateDirectory(dividendsFolder);
            Console.WriteLine($"[FileService] Created Dividends folder: {dividendsFolder}");
        }
    }

    public static void SaveDividends(string osEnginePath, string security, List<WikiDividend> dividends)
    {
        string? directory = Path.GetDirectoryName(osEnginePath);

        if (string.IsNullOrEmpty(directory))
        {
            throw new InvalidOperationException("Cannot determine OsEngine directory");
        }

        string filePath = Path.Combine(directory, "Wiki", "Dividends", $"{security}.md");

        var historical = dividends.Where(d => !d.IsFuture).OrderBy(d => d.RegistryCloseDate).ToList();
        var future = dividends.Where(d => d.IsFuture).OrderBy(d => d.RegistryCloseDate).ToList();

        var builder = new StringBuilder();
        builder.AppendLine($"# Dividends: {security}");
        builder.AppendLine();
        builder.AppendLine("## Metadata");
        builder.AppendLine();
        builder.AppendLine("| Field | Value |");
        builder.AppendLine("|---|---|");
        builder.AppendLine($"| Security | {security} |");
        builder.AppendLine($"| LastUpdated | {DateTime.Now:dd.MM.yyyy} |");
        builder.AppendLine($"| Source | https://smart-lab.ru/q/{security}/dividend/ |");
        builder.AppendLine();
        builder.AppendLine("## Historical Dividends");
        builder.AppendLine();
        builder.AppendLine("| Year | RegistryCloseDate | DividendAmount | DividendYield |");
        builder.AppendLine("|---|---|---|---|");

        foreach (WikiDividend dividend in historical)
        {
            builder.AppendLine($"| {dividend.Year} | {dividend.RegistryCloseDate:dd.MM.yyyy} | {FormatDecimal(dividend.DividendAmount)} | {FormatDecimal(dividend.DividendYield)} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Future Registry Close Dates");
        builder.AppendLine();
        builder.AppendLine("| Year | RegistryCloseDate | DividendAmount | DividendYield |");
        builder.AppendLine("|---|---|---|---|");

        foreach (WikiDividend dividend in future)
        {
            builder.AppendLine($"| {dividend.Year} | {dividend.RegistryCloseDate:dd.MM.yyyy} | {FormatDecimal(dividend.DividendAmount)} | {FormatDecimal(dividend.DividendYield)} |");
        }

        File.WriteAllText(filePath, builder.ToString(), Encoding.UTF8);
        Console.WriteLine($"[FileService] Saved dividends: {filePath}");
    }

    private static string FormatDecimal(decimal? value)
    {
        if (value == null)
        {
            return "TBD";
        }

        return value.Value.ToString(CultureInfo.InvariantCulture);
    }
}
