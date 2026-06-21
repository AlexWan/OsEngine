/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

namespace OsEngine.MCP
{
    /// <summary>
    /// DTO for terminal prime settings (PrimeSettingsMaster).
    /// </summary>
    public class McpPrimeSettings
    {
        public bool ErrorLogMessageBoxIsActive { get; set; }

        public bool ErrorLogBeepIsActive { get; set; }

        public bool TransactionBeepIsActive { get; set; }

        public bool RebootTradeUiLight { get; set; }

        public bool ReportCriticalErrors { get; set; }

        public string LabelInHeaderBotStation { get; set; }

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
