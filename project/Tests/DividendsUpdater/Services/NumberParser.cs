using System.Globalization;
using System.Text.RegularExpressions;

namespace DividendsUpdater.Services;

public static class NumberParser
{
    public static decimal ToDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        string normalized = value.Trim()
            .Replace("₽", string.Empty)
            .Replace("$", string.Empty)
            .Replace("€", string.Empty)
            .Replace("%", string.Empty)
            .Replace(" ", string.Empty)
            .Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator);

        if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result))
        {
            return result;
        }

        return 0;
    }

    public static decimal? ToNullableDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim()
            .Replace("₽", string.Empty)
            .Replace("$", string.Empty)
            .Replace("€", string.Empty)
            .Replace("%", string.Empty)
            .Replace(" ", string.Empty)
            .Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator);

        if (normalized.Equals("TBD", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result))
        {
            return result;
        }

        return null;
    }

    public static int ExtractYear(string? period)
    {
        if (string.IsNullOrWhiteSpace(period))
        {
            return 0;
        }

        // Ищем последовательность из 4 цифр подряд (год)
        Match match = Regex.Match(period, @"\d{4}");

        if (match.Success && int.TryParse(match.Value, out int year))
        {
            return year;
        }

        return 0;
    }
}
