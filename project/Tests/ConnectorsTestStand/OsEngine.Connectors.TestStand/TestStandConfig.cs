/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.IO;
using System.Text.Json;

namespace OsEngine.Connectors.TestStand
{
    /// <summary>
    /// Run configuration of the test stand. Loaded from test-stand-config.json
    /// next to the executable. A missing file or missing fields fall back to
    /// the built-in defaults. Command line arguments override the file values.
    /// Secrets (the connector token) are NOT stored here - see the token file
    /// (TokenFileName, by default tinvest-token.txt).
    /// </summary>
    public class TestStandConfig
    {
        /// <summary>
        /// Path to OsEngine.exe. Empty = default path relative to the executable.
        /// </summary>
        public string OsEnginePath { get; set; } = string.Empty;

        public int Port { get; set; } = 6500;

        public string ApiKey { get; set; } = "osengine-mcp-default-key";

        public int TimeoutSeconds { get; set; } = 60;

        /// <summary>
        /// MCP transport: "v2" (streamable HTTP, default) or "v1" (legacy).
        /// </summary>
        public string Transport { get; set; } = "v2";

        public ServerTestsConfig ServerTests { get; set; } = new ServerTestsConfig();

        public class ServerTestsConfig
        {
            /// <summary>
            /// Server type from ServerType enum (TInvest, BinanceFutures, ...).
            /// </summary>
            public string ServerType { get; set; } = "TInvest";

            /// <summary>
            /// Token file next to the executable. A single line with the token.
            /// Must NOT be committed to the repository.
            /// </summary>
            public string TokenFileName { get; set; } = "tinvest-token.txt";

            public string SecurityName { get; set; } = "SBER";

            public string SecurityClass { get; set; } = "Stock rub";

            /// <summary>
            /// Securities list for multi-security tests (V2, V3, D4, D5, C4),
            /// joined with SecuritiesSeparator. C4 requires at least 5 securities.
            /// </summary>
            public string SecuritiesList { get; set; } = "SBER_GAZP_LKOH_SBERP_MGNT";

            public string SecuritiesSeparator { get; set; } = "_";

            /// <summary>
            /// Asset name in the portfolio for the P1 test (Portfolio_1_Validation).
            /// </summary>
            public string AssetInPortfolio { get; set; } = "SBER";

            public decimal Volume { get; set; } = 3m;

            /// <summary>
            /// Orders count for spam-type tests (O4, O5, O7, O12, O14).
            /// </summary>
            public int CountOrders { get; set; } = 5;

            /// <summary>
            /// Securities count for the C5 screener test. The robot requires at least 15.
            /// </summary>
            public int SecuritiesCount { get; set; } = 15;

            /// <summary>
            /// Work time in minutes for streaming tests (V2, V3, C5).
            /// </summary>
            public int MinutesToTest { get; set; } = 1;

            /// <summary>
            /// TimeFrame for the C5 screener test (Min1, Min5, ...).
            /// </summary>
            public string TimeFrame { get; set; } = "Min1";

            /// <summary>
            /// Base date for data request tests (D1, D4). Empty = robot default (now).
            /// </summary>
            public string StartDate { get; set; } = string.Empty;

            public string TesterBotName { get; set; } = "ConnectorsTesterBot";

            public int ConnectTimeoutSeconds { get; set; } = 120;

            public int SecuritiesTimeoutSeconds { get; set; } = 120;

            public int TestTimeoutMinutes { get; set; } = 25;

            /// <summary>
            /// Timeout in minutes for waiting the "Tests started" marker in the
            /// robot log after the test button click. No marker = the test did
            /// not start and is marked as failed.
            /// </summary>
            public int TestStartTimeoutMinutes { get; set; } = 2;
        }

        private const string FileName = "test-stand-config.json";

        /// <summary>
        /// Load the configuration from test-stand-config.json next to the executable.
        /// Returns an instance with built-in defaults when the file is missing or broken.
        /// </summary>
        public static TestStandConfig Load(string baseDirectory)
        {
            string filePath = Path.Combine(baseDirectory, FileName);

            try
            {
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"[Config] {FileName} not found next to the executable. Built-in defaults are used.");
                    return new TestStandConfig();
                }

                string json = File.ReadAllText(filePath);

                TestStandConfig? config = JsonSerializer.Deserialize<TestStandConfig>(json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    });

                if (config == null)
                {
                    Console.WriteLine($"[Config] {FileName} is empty. Built-in defaults are used.");
                    return new TestStandConfig();
                }

                config.ServerTests ??= new ServerTestsConfig();

                Console.WriteLine($"[Config] Loaded from {filePath}");

                return config;
            }
            catch (Exception error)
            {
                Console.WriteLine($"[Config] Failed to load {filePath}: {error.Message}. Built-in defaults are used.");
                return new TestStandConfig();
            }
        }
    }
}
