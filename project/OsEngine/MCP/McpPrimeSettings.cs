/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Text.Json.Serialization;

namespace OsEngine.MCP
{
    /// <summary>
    /// DTO for terminal prime settings (PrimeSettingsMaster).
    /// </summary>
    public class McpPrimeSettings
    {
        [JsonPropertyName("errorLogMessageBoxIsActive")]
        public bool ErrorLogMessageBoxIsActive { get; set; }

        [JsonPropertyName("errorLogBeepIsActive")]
        public bool ErrorLogBeepIsActive { get; set; }

        [JsonPropertyName("transactionBeepIsActive")]
        public bool TransactionBeepIsActive { get; set; }

        [JsonPropertyName("rebootTradeUiLight")]
        public bool RebootTradeUiLight { get; set; }

        [JsonPropertyName("reportCriticalErrors")]
        public bool ReportCriticalErrors { get; set; }

        [JsonPropertyName("labelInHeaderBotStation")]
        public string LabelInHeaderBotStation { get; set; }

        [JsonPropertyName("memoryCleanerRegime")]
        public string MemoryCleanerRegime { get; set; }

        /// <summary>
        /// Create DTO from current PrimeSettingsMaster values.
        /// </summary>
        public static McpPrimeSettings FromCurrent()
        {
            return new McpPrimeSettings
            {
                ErrorLogMessageBoxIsActive = PrimeSettings.PrimeSettingsMaster.ErrorLogMessageBoxIsActive,
                ErrorLogBeepIsActive = PrimeSettings.PrimeSettingsMaster.ErrorLogBeepIsActive,
                TransactionBeepIsActive = PrimeSettings.PrimeSettingsMaster.TransactionBeepIsActive,
                RebootTradeUiLight = PrimeSettings.PrimeSettingsMaster.RebootTradeUiLight,
                ReportCriticalErrors = PrimeSettings.PrimeSettingsMaster.ReportCriticalErrors,
                LabelInHeaderBotStation = PrimeSettings.PrimeSettingsMaster.LabelInHeaderBotStation,
                MemoryCleanerRegime = PrimeSettings.PrimeSettingsMaster.MemoryCleanerRegime.ToString()
            };
        }
    }
}
