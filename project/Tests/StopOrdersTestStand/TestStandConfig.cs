/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.IO;
using System.Text.Json;

namespace StopOrdersTestStand
{
    /// <summary>
    /// Run configuration of the test stand. Loaded from test-stand-config.json
    /// next to the executable. A missing file or missing fields fall back to
    /// the built-in defaults. Command line arguments override the file values.
    /// Secrets (the T-Invest token) are NOT stored here - see tinvest-token.txt.
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

        public ServerTestsConfig ServerTests { get; set; } = new ServerTestsConfig();

        public class ServerTestsConfig
        {
            public string ServerType { get; set; } = "TInvest";

            public string SecurityName { get; set; } = "SBER";

            public string SecurityClass { get; set; } = "Stock rub";

            public decimal Volume { get; set; } = 3m;

            public string TesterBotName { get; set; } = "StopServerTesterBot";

            public int Orders14CountOrders { get; set; } = 20;

            public int ConnectTimeoutSeconds { get; set; } = 120;

            public int SecuritiesTimeoutSeconds { get; set; } = 120;

            public int TestTimeoutMinutes { get; set; } = 25;
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
