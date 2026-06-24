/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple.pdf
*/

using System.Text.Json.Serialization;

namespace WikiConnectionTest.Models
{
    /// <summary>
    /// Security description stored in Wiki files.
    /// Uses two schemas: tradeSecurity and dataSecurity.
    /// </summary>
    public class WikiSecurity
    {
        [JsonPropertyName("schema")]
        public string Schema { get; set; } = "dataSecurity";

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("nameClass")]
        public string NameClass { get; set; } = string.Empty;

        [JsonPropertyName("nameFull")]
        public string NameFull { get; set; } = string.Empty;

        [JsonPropertyName("nameId")]
        public string NameId { get; set; } = string.Empty;

        [JsonPropertyName("exchange")]
        public string Exchange { get; set; } = string.Empty;

        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;

        [JsonPropertyName("securityType")]
        public string SecurityType { get; set; } = string.Empty;

        /// <summary>
        /// Additional fields returned by the connector but not mapped explicitly.
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, System.Text.Json.JsonElement>? ExtensionData { get; set; }
    }
}
