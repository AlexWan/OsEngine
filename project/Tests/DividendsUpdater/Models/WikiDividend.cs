namespace DividendsUpdater.Models;

public class WikiDividend
{
    public int Year { get; set; }

    public DateTime RegistryCloseDate { get; set; }

    public decimal? DividendAmount { get; set; }

    public decimal? DividendYield { get; set; }

    public bool IsFuture { get; set; }
}
