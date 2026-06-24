/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple.pdf
*/

using System.Text.Json.Serialization;

namespace WikiConnectionTest.Models
{
    /// <summary>
    /// Metadata written at the top of each Wiki security file.
    /// </summary>
    public class ConnectorMetadata
    {
        [JsonPropertyName("connector")]
        public string Connector { get; set; } = string.Empty;

        [JsonPropertyName("collectedAt")]
        public string CollectedAt { get; set; } = string.Empty;

        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        [JsonPropertyName("permissions")]
        public ConnectorPermissions Permissions { get; set; } = new ConnectorPermissions();
    }

    public class ConnectorPermissions
    {
        [JsonPropertyName("isTradingSupported")]
        public bool IsTradingSupported { get; set; }

        [JsonPropertyName("isDataFeedSupported")]
        public bool IsDataFeedSupported { get; set; }

        [JsonPropertyName("tradeTimeFrames")]
        public List<string> TradeTimeFrames { get; set; } = new List<string>();

        [JsonPropertyName("dataFeedTimeFrames")]
        public List<string> DataFeedTimeFrames { get; set; } = new List<string>();
    }
}
