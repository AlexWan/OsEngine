/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple.pdf
*/

using System.Diagnostics;
using System.Text.Json;
using WikiConnectionTest.Models;
using WikiConnectionTest.Services;

namespace WikiConnectionTest
{
    class Program
    {
        static int Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.InputEncoding = System.Text.Encoding.UTF8;

            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            AppSettings settings = AppSettingsService.Load(baseDirectory);
            ConnectionSecrets secrets = SecretsService.Load(baseDirectory);

            OsEngineProcessService? processService = null;
            McpApiClient? client = null;

            Stopwatch stopwatch = Stopwatch.StartNew();
            var results = new List<ConnectorResult>();
            bool anySuccess = false;

            try
            {
                processService = new OsEngineProcessService(settings.OsEnginePath);

                client = TryConnect(settings);

                if (client == null)
                {
                    processService.EnsureRunning();
                    client = WaitForMcpClient(settings);
                }

                var mcp = new McpService(client);
                var collector = new SecurityCollector(mcp, TimeSpan.FromSeconds(settings.SecurityLoadTimeoutSeconds));
                var fileService = new WikiFileService();

                ConnectorConfig[] connectors = GetConnectorConfigs(secrets);

                foreach (ConnectorConfig config in connectors)
                {
                    ConnectorResult result = ProcessConnector(config, secrets, mcp, collector, fileService, settings.OsEnginePath);
                    results.Add(result);

                    if (result.Success)
                    {
                        anySuccess = true;
                    }
                }
            }
            catch (Exception error)
            {
                Console.WriteLine($"[Error] {error}");
            }
            finally
            {
                stopwatch.Stop();

                client?.Dispose();
                processService?.Stop();
            }

            PrintReport(results, stopwatch.Elapsed);
            ConsoleHelper.WaitIfRunByUser();
            return anySuccess ? 0 : 1;
        }

        private static ConnectorConfig[] GetConnectorConfigs(ConnectionSecrets secrets)
        {
            var configs = new List<ConnectorConfig>
            {
                new ConnectorConfig("MoexDataServer", "dataSecurity", false),
                new ConnectorConfig("QscalpMarketDepth", "dataSecurity", false)
            };

            if (secrets.Connectors.ContainsKey("TInvest"))
            {
                configs.Add(new ConnectorConfig("TInvest", "tradeSecurity", true));
            }

            if (secrets.Connectors.ContainsKey("Alor"))
            {
                configs.Add(new ConnectorConfig("Alor", "tradeSecurity", true));
            }

            return configs.ToArray();
        }

        private static ConnectorResult ProcessConnector(ConnectorConfig config, ConnectionSecrets secrets, McpService mcp,
            SecurityCollector collector, WikiFileService fileService, string osEnginePath)
        {
            Console.WriteLine();
            Console.WriteLine($"=== Processing {config.Type} ===");

            var result = new ConnectorResult { Type = config.Type };

            try
            {
                Dictionary<string, string>? connectorSecrets = secrets.Connectors.TryGetValue(config.Type, out Dictionary<string, string>? value)
                    ? value
                    : null;

                ValidateConnectorSecrets(config.Type, connectorSecrets);

                IEnumerable<KeyValuePair<string, object>>? parameters = ConvertParameters(connectorSecrets);

                List<WikiSecurity> securities = collector.Collect(config.Type, config.Schema, parameters);
                result.Count = securities.Count;

                ConnectorMetadata metadata = BuildMetadata(mcp, config.Type);
                string wikiFolder = fileService.EnsureWikiFolder(osEnginePath);
                string filePath = Path.Combine(wikiFolder, $"{GetFileNamePrefix(config.Type)}_securities.md");

                fileService.SaveSecurities(filePath, metadata, securities);

                result.Success = true;
                result.FilePath = filePath;
            }
            catch (Exception error)
            {
                result.ErrorMessage = error.Message;
                Console.WriteLine($"[Error] {config.Type}: {error.Message}");
            }

            return result;
        }

        private static IEnumerable<KeyValuePair<string, object>>? ConvertParameters(Dictionary<string, string>? connectorSecrets)
        {
            if (connectorSecrets == null || connectorSecrets.Count == 0)
            {
                return null;
            }

            return connectorSecrets.Select(p => new KeyValuePair<string, object>(p.Key, p.Value));
        }

        private static void ValidateConnectorSecrets(string connectorType, Dictionary<string, string>? secrets)
        {
            if (connectorType == "Alor")
            {
                if (secrets == null
                    || !secrets.TryGetValue("Token", out string? token)
                    || string.IsNullOrEmpty(token))
                {
                    throw new InvalidOperationException("Alor requires a Token in connection-secrets.json.");
                }

                bool hasPortfolio = secrets.ContainsKey("Portfolio Spot") && !string.IsNullOrEmpty(secrets["Portfolio Spot"])
                    || secrets.ContainsKey("Portfolio FORTS") && !string.IsNullOrEmpty(secrets["Portfolio FORTS"])
                    || secrets.ContainsKey("Portfolio currency") && !string.IsNullOrEmpty(secrets["Portfolio currency"])
                    || secrets.ContainsKey("Portfolio spare") && !string.IsNullOrEmpty(secrets["Portfolio spare"]);

                if (!hasPortfolio)
                {
                    throw new InvalidOperationException(
                        "Alor requires at least one portfolio name: Portfolio Spot, Portfolio FORTS, Portfolio currency or Portfolio spare. " +
                        "Portfolio names can be found on the Alor website.");
                }
            }
        }

        private static McpApiClient? TryConnect(AppSettings settings)
        {
            try
            {
                var client = new McpApiClient(settings.McpBaseUrl, settings.McpApiKey);
                client.Initialize();
                Console.WriteLine("[MCP] Connected to existing OsEngine instance.");
                return client;
            }
            catch
            {
                return null;
            }
        }

        private static McpApiClient WaitForMcpClient(AppSettings settings)
        {
            var client = new McpApiClient(settings.McpBaseUrl, settings.McpApiKey);
            client.WaitForReady(TimeSpan.FromSeconds(settings.McpReadyTimeoutSeconds));
            Console.WriteLine("[MCP] OsEngine MCP API is ready.");
            return client;
        }

        private static ConnectorMetadata BuildMetadata(McpService mcp, string connectorType)
        {
            var metadata = new ConnectorMetadata
            {
                Connector = connectorType,
                CollectedAt = DateTimeOffset.Now.ToString("O"),
                Source = "server_instance_get_securities",
                Permissions = new ConnectorPermissions
                {
                    IsTradingSupported = false,
                    IsDataFeedSupported = true,
                    TradeTimeFrames = new List<string>(),
                    DataFeedTimeFrames = new List<string>()
                }
            };

            try
            {
                JsonElement permissionsResponse = mcp.GetConnectorPermissions(connectorType);

                if (permissionsResponse.TryGetProperty("permissions", out JsonElement permissionsElement)
                    && permissionsElement.ValueKind == JsonValueKind.Object)
                {
                    metadata.Permissions = ParsePermissions(permissionsElement);
                }
            }
            catch (Exception error)
            {
                Console.WriteLine($"[Metadata] Failed to read connector permissions: {error.Message}");
            }

            return metadata;
        }

        private static ConnectorPermissions ParsePermissions(JsonElement permissionsElement)
        {
            return new ConnectorPermissions
            {
                IsTradingSupported = GetBoolProperty(permissionsElement, "MarketOrdersIsSupport"),
                IsDataFeedSupported = HasAnyDataFeedPermission(permissionsElement),
                TradeTimeFrames = ExtractTradeTimeFrames(permissionsElement),
                DataFeedTimeFrames = ExtractDataFeedTimeFrames(permissionsElement)
            };
        }

        private static bool GetBoolProperty(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out JsonElement property)
                && property.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            return false;
        }

        private static bool HasAnyDataFeedPermission(JsonElement element)
        {
            foreach (string propertyName in DataFeedPermissionMapping.Keys)
            {
                if (GetBoolProperty(element, propertyName))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<string> ExtractTradeTimeFrames(JsonElement element)
        {
            var result = new List<string>();

            if (!element.TryGetProperty("TradeTimeFramePermission", out JsonElement tradePermission)
                || tradePermission.ValueKind != JsonValueKind.Object)
            {
                return result;
            }

            var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["TimeFrameSec1IsOn"] = "1sec",
                ["TimeFrameSec2IsOn"] = "2sec",
                ["TimeFrameSec5IsOn"] = "5sec",
                ["TimeFrameSec10IsOn"] = "10sec",
                ["TimeFrameSec15IsOn"] = "15sec",
                ["TimeFrameSec20IsOn"] = "20sec",
                ["TimeFrameSec30IsOn"] = "30sec",
                ["TimeFrameMin1IsOn"] = "1min",
                ["TimeFrameMin2IsOn"] = "2min",
                ["TimeFrameMin3IsOn"] = "3min",
                ["TimeFrameMin5IsOn"] = "5min",
                ["TimeFrameMin10IsOn"] = "10min",
                ["TimeFrameMin15IsOn"] = "15min",
                ["TimeFrameMin20IsOn"] = "20min",
                ["TimeFrameMin30IsOn"] = "30min",
                ["TimeFrameMin45IsOn"] = "45min",
                ["TimeFrameHour1IsOn"] = "1hour",
                ["TimeFrameHour2IsOn"] = "2hour",
                ["TimeFrameHour4IsOn"] = "4hour",
                ["TimeFrameDayIsOn"] = "1day"
            };

            foreach (KeyValuePair<string, string> pair in mapping)
            {
                if (GetBoolProperty(tradePermission, pair.Key))
                {
                    result.Add(pair.Value);
                }
            }

            return result;
        }

        private static readonly Dictionary<string, string> DataFeedPermissionMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DataFeedTfTickCanLoad"] = "tick",
            ["DataFeedTf1SecondCanLoad"] = "1sec",
            ["DataFeedTf2SecondCanLoad"] = "2sec",
            ["DataFeedTf5SecondCanLoad"] = "5sec",
            ["DataFeedTf10SecondCanLoad"] = "10sec",
            ["DataFeedTf15SecondCanLoad"] = "15sec",
            ["DataFeedTf20SecondCanLoad"] = "20sec",
            ["DataFeedTf30SecondCanLoad"] = "30sec",
            ["DataFeedTf1MinuteCanLoad"] = "1min",
            ["DataFeedTf2MinuteCanLoad"] = "2min",
            ["DataFeedTf5MinuteCanLoad"] = "5min",
            ["DataFeedTf10MinuteCanLoad"] = "10min",
            ["DataFeedTf15MinuteCanLoad"] = "15min",
            ["DataFeedTf30MinuteCanLoad"] = "30min",
            ["DataFeedTf1HourCanLoad"] = "1hour",
            ["DataFeedTf2HourCanLoad"] = "2hour",
            ["DataFeedTf4HourCanLoad"] = "4hour",
            ["DataFeedTfDayCanLoad"] = "1day",
            ["DataFeedTfMarketDepthCanLoad"] = "marketDepth",
            ["DataFeedTfMarketDepthHistoryCanLoad"] = "marketDepthHistory"
        };

        private static List<string> ExtractDataFeedTimeFrames(JsonElement element)
        {
            var result = new List<string>();

            foreach (KeyValuePair<string, string> pair in DataFeedPermissionMapping)
            {
                if (GetBoolProperty(element, pair.Key))
                {
                    result.Add(pair.Value);
                }
            }

            return result;
        }

        private static string GetFileNamePrefix(string connectorType)
        {
            switch (connectorType)
            {
                case "MoexDataServer":
                    return "moex_iss";
                case "QscalpMarketDepth":
                    return "qscalp";
                case "Alor":
                    return "alor";
                case "TInvest":
                    return "tinvest";
                default:
                    return connectorType.ToLowerInvariant();
            }
        }

        private static void PrintReport(List<ConnectorResult> results, TimeSpan elapsed)
        {
            Console.WriteLine();
            Console.WriteLine("=== WikiConnectionTest Report ===");
            Console.WriteLine($"Elapsed: {elapsed.TotalSeconds:F1}s");
            Console.WriteLine();

            foreach (ConnectorResult result in results)
            {
                string status = result.Success ? "OK" : "FAILED";
                Console.WriteLine($"Connector: {result.Type,-25} Status: {status,-6} Count: {result.Count}");

                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    Console.WriteLine($"  Error: {result.ErrorMessage}");
                }
            }

            int successCount = results.Count(r => r.Success);
            Console.WriteLine();
            Console.WriteLine($"Total: {successCount}/{results.Count} connectors succeeded");
        }

        private class ConnectorConfig
        {
            public string Type { get; }
            public string Schema { get; }
            public bool RequiresSecrets { get; }

            public ConnectorConfig(string type, string schema, bool requiresSecrets)
            {
                Type = type;
                Schema = schema;
                RequiresSecrets = requiresSecrets;
            }
        }

        private class ConnectorResult
        {
            public string Type { get; set; } = string.Empty;
            public bool Success { get; set; }
            public int Count { get; set; }
            public string? FilePath { get; set; }
            public string? ErrorMessage { get; set; }
        }
    }
}
