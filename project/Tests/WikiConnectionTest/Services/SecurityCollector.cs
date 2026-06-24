/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple.pdf
*/

using System.Text.Json;
using WikiConnectionTest.Models;

namespace WikiConnectionTest.Services
{
    /// <summary>
    /// Collects securities from a single connector via MCP API.
    /// </summary>
    public class SecurityCollector
    {
        private readonly McpService _mcp;
        private readonly TimeSpan _statusTimeout;
        private readonly TimeSpan _statusPollInterval;

        public SecurityCollector(McpService mcp, TimeSpan? statusTimeout = null)
        {
            _mcp = mcp ?? throw new ArgumentNullException(nameof(mcp));
            _statusTimeout = statusTimeout ?? TimeSpan.FromSeconds(60);
            _statusPollInterval = TimeSpan.FromSeconds(1);
        }

        public List<WikiSecurity> Collect(string connectorType, string schema = "dataSecurity", IEnumerable<KeyValuePair<string, object>>? parameters = null)
        {
            Console.WriteLine($"[Collector] Activating connector {connectorType}...");
            _mcp.ActivateServer(connectorType);

            int number;

            try
            {
                Console.WriteLine($"[Collector] Creating instance...");
                number = _mcp.CreateInstance(connectorType);
            }
            catch (InvalidOperationException error)
                when (error.Message.Contains("does not support multiple instances", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[Collector] Connector {connectorType} does not support multiple instances. Using default instance #0.");
                number = 0;
            }

            try
            {
                var mergedParameters = MergeSectorParameters(connectorType, number, parameters);

                if (mergedParameters != null && mergedParameters.Any())
                {
                    Console.WriteLine($"[Collector] Setting parameters...");
                    _mcp.SetParams(connectorType, number, mergedParameters);
                }

                Console.WriteLine($"[Collector] Connecting instance #{number}...");
                _mcp.ConnectInstance(connectorType, number);

                WaitForStatus(connectorType, number, "Connect");

                Console.WriteLine($"[Collector] Loading securities...");
                List<JsonElement> securities = WaitForSecurities(connectorType, number);

                Console.WriteLine($"[Collector] Received {securities.Count} securities");

                return securities.Select(s => ConvertToWikiSecurity(s, schema)).ToList();
            }
            finally
            {
                try
                {
                    Console.WriteLine($"[Collector] Disconnecting instance #{number}...");
                    _mcp.DisconnectInstance(connectorType, number);
                }
                catch (Exception error)
                {
                    Console.WriteLine($"[Collector] Disconnect failed: {error.Message}");
                }

                try
                {
                    Console.WriteLine($"[Collector] Deleting instance #{number}...");
                    _mcp.DeleteInstance(connectorType, number);
                }
                catch (Exception error)
                {
                    Console.WriteLine($"[Collector] Delete failed: {error.Message}");
                }
            }
        }

        private void WaitForStatus(string connectorType, int number, string expectedStatus)
        {
            DateTime deadline = DateTime.Now.Add(_statusTimeout);

            while (DateTime.Now < deadline)
            {
                string status = _mcp.GetStatus(connectorType, number);

                if (string.Equals(status, expectedStatus, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"[Collector] Instance #{number} status: {status}");
                    return;
                }

                Thread.Sleep(_statusPollInterval);
            }

            throw new TimeoutException($"Connector {connectorType} instance #{number} did not reach status {expectedStatus} within {_statusTimeout}");
        }

        private List<JsonElement> WaitForSecurities(string connectorType, int number)
        {
            DateTime deadline = DateTime.Now.Add(_statusTimeout);

            while (DateTime.Now < deadline)
            {
                List<JsonElement> securities = _mcp.GetSecurities(connectorType, number);

                if (securities.Count > 0)
                {
                    return securities;
                }

                Thread.Sleep(_statusPollInterval);
            }

            Console.WriteLine("[Collector] Securities did not arrive within timeout. Returning empty list.");
            return _mcp.GetSecurities(connectorType, number);
        }

        private List<KeyValuePair<string, object>>? MergeSectorParameters(string connectorType, int number,
            IEnumerable<KeyValuePair<string, object>>? parameters)
        {
            var result = parameters?.ToList() ?? new List<KeyValuePair<string, object>>();

            if (connectorType != "TInvest" && connectorType != "Alor")
            {
                return result;
            }

            try
            {
                List<(string Name, string Type)> serverParams = _mcp.GetServerParams(connectorType, number);

                var sectorKeywords = new List<string>
                {
                    "Stock", "Акции",
                    "Futures", "Фьючерсы",
                    "Options", "Опционы",
                    "Other", "Другое"
                };

                if (connectorType == "Alor")
                {
                    sectorKeywords.Add("Currency");
                    sectorKeywords.Add("Валюты");
                }

                foreach ((string Name, string Type) param in serverParams)
                {
                    if (param.Type != "Bool")
                    {
                        continue;
                    }

                    bool isSector = sectorKeywords.Any(keyword =>
                        param.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);

                    if (!isSector)
                    {
                        continue;
                    }

                    int existingIndex = result.FindIndex(p =>
                        string.Equals(p.Key, param.Name, StringComparison.OrdinalIgnoreCase));

                    if (existingIndex >= 0)
                    {
                        result[existingIndex] = new KeyValuePair<string, object>(param.Name, true);
                    }
                    else
                    {
                        result.Add(new KeyValuePair<string, object>(param.Name, true));
                    }
                }
            }
            catch (Exception error)
            {
                Console.WriteLine($"[Collector] Failed to enable sector parameters: {error.Message}");
            }

            return result;
        }

        private static WikiSecurity ConvertToWikiSecurity(JsonElement element, string schema)
        {
            var security = new WikiSecurity
            {
                Schema = schema
            };

            security.Name = GetStringProperty(element, "name");
            security.NameClass = GetStringProperty(element, "nameClass");
            security.NameFull = GetStringProperty(element, "nameFull");
            security.NameId = GetStringProperty(element, "nameId");
            security.Exchange = GetStringProperty(element, "exchange");
            security.State = GetStringProperty(element, "state");
            security.SecurityType = GetStringProperty(element, "securityType");

            return security;
        }

        private static string GetStringProperty(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out JsonElement property)
                && property.ValueKind == JsonValueKind.String)
            {
                return property.GetString() ?? string.Empty;
            }

            return string.Empty;
        }
    }
}
