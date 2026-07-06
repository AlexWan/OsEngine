namespace DividendsUpdater.Models;

public class AppSettings
{
    public string OsEnginePath { get; set; } = string.Empty;
    public int RequestDelayMs { get; set; } = 500;
    public int HttpTimeoutSeconds { get; set; } = 30;
}
