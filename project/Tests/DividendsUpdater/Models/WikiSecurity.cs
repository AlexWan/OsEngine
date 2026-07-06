using System.Text.Json.Serialization;

namespace DividendsUpdater.Models;

public class WikiSecurity
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("nameClass")]
    public string NameClass { get; set; } = string.Empty;

    [JsonPropertyName("nameFull")]
    public string NameFull { get; set; } = string.Empty;

    [JsonPropertyName("securityType")]
    public string SecurityType { get; set; } = string.Empty;

    [JsonPropertyName("exchange")]
    public string Exchange { get; set; } = string.Empty;
}
