/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Text.Json.Serialization;

namespace OsEngine.MCP
{
    /// <summary>
    /// Current status of the OsEngine terminal exposed via MCP API.
    /// </summary>
    public class McpTerminalStatus
    {
        [JsonPropertyName("mode")]
        public string Mode { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("processStarted")]
        public DateTime ProcessStarted { get; set; }

        [JsonPropertyName("isMainWindowVisible")]
        public bool IsMainWindowVisible { get; set; }
    }
}
