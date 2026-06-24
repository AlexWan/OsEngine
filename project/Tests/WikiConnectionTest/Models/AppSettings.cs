/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple.pdf
*/

namespace WikiConnectionTest.Models
{
    /// <summary>
    /// Application settings for WikiConnectionTest.
    /// </summary>
    public class AppSettings
    {
        public string OsEnginePath { get; set; } = string.Empty;

        public string McpBaseUrl { get; set; } = "http://localhost:6500";

        public string McpApiKey { get; set; } = "osengine-mcp-default-key";

        public int McpReadyTimeoutSeconds { get; set; } = 60;

        public int SecurityLoadTimeoutSeconds { get; set; } = 300;
    }
}
