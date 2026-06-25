/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace OsEngine.McpApi.TestStand.Tests
{
    /// <summary>
    /// Tests for security wiki reference methods.
    /// </summary>
    public class WikiSecuritiesTests
    {
        private const string Module = "WIKI_SECURITIES";
        private const string KnownTicker = "SBER";
        private const string UnknownTicker = "ThisTickerDoesNotExist12345";

        private readonly TestContext _context;

        public WikiSecuritiesTests(TestContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void RunAll()
        {
            _context.PrintModuleHeader(Module);

            TestToolsListContainsWikiSecuritiesMethods();
            TestMoexIssList();
            TestTinvestList();
            TestAlorList();
            TestQscalpList();
            TestListFilter();
            TestMappingSearchAcrossAllConnectors();
            TestMappingSearchByConnector();
            TestMappingSearchExact();
            TestMappingSearchLimit();
            TestMappingSearchNotFound();
            TestMappingSearchEmptyQuery();
        }

        private void TestToolsListContainsWikiSecuritiesMethods()
        {
            const string method = "tools/list";
            object request = new { };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsList();
                _context.PrintResponse(response);

                using (JsonDocument document = JsonDocument.Parse(response))
                {
                    JsonElement root = document.RootElement;

                    if (!root.TryGetProperty("Tools", out JsonElement tools) || tools.GetArrayLength() == 0)
                    {
                        _context.RecordFail(Module, method, "Tools array is empty or missing");
                        return;
                    }

                    string[] toolNames = tools.EnumerateArray()
                        .Select(t => t.GetProperty("name").GetString() ?? string.Empty)
                        .ToArray();

                    string[] expectedTools =
                    {
                        "wiki_securities_moex_iss",
                        "wiki_securities_tinvest",
                        "wiki_securities_alor",
                        "wiki_securities_qscalp",
                        "wiki_securities_mapping_info"
                    };

                    foreach (string expected in expectedTools)
                    {
                        if (!toolNames.Contains(expected))
                        {
                            _context.RecordFail(Module, method, $"{expected} tool is missing");
                            return;
                        }
                    }

                    _context.RecordPass(Module, method, "all wiki securities tools registered");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestMoexIssList()
        {
            TestList("wiki_securities_moex_iss", "MoexDataServer");
        }

        private void TestTinvestList()
        {
            TestList("wiki_securities_tinvest", "TInvest");
        }

        private void TestAlorList()
        {
            TestList("wiki_securities_alor", "Alor");
        }

        private void TestQscalpList()
        {
            TestList("wiki_securities_qscalp", "QscalpMarketDepth");
        }

        private void TestList(string method, string expectedConnector)
        {
            object request = new { };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                string resultJson = ExtractToolResult(response);

                using (JsonDocument document = JsonDocument.Parse(resultJson))
                {
                    JsonElement root = document.RootElement;

                    if (!root.TryGetProperty("securities", out JsonElement securities)
                        || securities.GetArrayLength() == 0)
                    {
                        _context.RecordFail(Module, method, "securities array is empty or missing");
                        return;
                    }

                    if (!root.TryGetProperty("count", out JsonElement count)
                        || count.ValueKind != JsonValueKind.Number
                        || count.GetInt32() == 0)
                    {
                        _context.RecordFail(Module, method, "count is missing or zero");
                        return;
                    }

                    if (!root.TryGetProperty("connector", out JsonElement connector)
                        || connector.ValueKind != JsonValueKind.String
                        || string.IsNullOrEmpty(connector.GetString()))
                    {
                        _context.RecordFail(Module, method, "connector is missing");
                        return;
                    }

                    if (!root.TryGetProperty("collected_at", out _))
                    {
                        _context.RecordFail(Module, method, "collected_at is missing");
                        return;
                    }

                    _context.RecordPass(Module, method, $"{expectedConnector} count={securities.GetArrayLength()}");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestListFilter()
        {
            const string method = "wiki_securities_moex_iss";
            object request = new { filter = KnownTicker };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                string resultJson = ExtractToolResult(response);

                using (JsonDocument document = JsonDocument.Parse(resultJson))
                {
                    JsonElement root = document.RootElement;

                    if (!root.TryGetProperty("securities", out JsonElement securities))
                    {
                        _context.RecordFail(Module, method, "securities array is missing");
                        return;
                    }

                    foreach (JsonElement security in securities.EnumerateArray())
                    {
                        string name = GetStringProperty(security, "name");
                        string nameClass = GetStringProperty(security, "nameClass");
                        string nameFull = GetStringProperty(security, "nameFull");
                        string nameId = GetStringProperty(security, "nameId");

                        string combined = name + nameClass + nameFull + nameId;

                        if (!combined.Contains(KnownTicker, StringComparison.OrdinalIgnoreCase))
                        {
                            _context.RecordFail(Module, method, $"filter returned unrelated security {name}");
                            return;
                        }
                    }

                    _context.RecordPass(Module, method, $"filtered count={securities.GetArrayLength()}");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestMappingSearchAcrossAllConnectors()
        {
            const string method = "wiki_securities_mapping_info";
            object request = new { query = KnownTicker };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                string resultJson = ExtractToolResult(response);

                using (JsonDocument document = JsonDocument.Parse(resultJson))
                {
                    JsonElement root = document.RootElement;

                    if (!root.TryGetProperty("query", out _))
                    {
                        _context.RecordFail(Module, method, "query is missing");
                        return;
                    }

                    if (!root.TryGetProperty("total", out JsonElement total)
                        || total.ValueKind != JsonValueKind.Number
                        || total.GetInt32() == 0)
                    {
                        _context.RecordFail(Module, method, "total is missing or zero");
                        return;
                    }

                    if (!root.TryGetProperty("results", out JsonElement results)
                        || results.GetArrayLength() == 0)
                    {
                        _context.RecordFail(Module, method, "results array is empty or missing");
                        return;
                    }

                    HashSet<string> connectors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (JsonElement item in results.EnumerateArray())
                    {
                        string connector = GetStringProperty(item, "connector");
                        string connectorShort = GetStringProperty(item, "connector_short");

                        if (string.IsNullOrEmpty(connector) || string.IsNullOrEmpty(connectorShort))
                        {
                            _context.RecordFail(Module, method, "connector or connector_short is missing");
                            return;
                        }

                        if (!item.TryGetProperty("is_trading_supported", out _)
                            || !item.TryGetProperty("is_data_feed_supported", out _))
                        {
                            _context.RecordFail(Module, method, "metadata flags are missing");
                            return;
                        }

                        if (!item.TryGetProperty("security", out _))
                        {
                            _context.RecordFail(Module, method, "security is missing");
                            return;
                        }

                        connectors.Add(connector);
                    }

                    _context.RecordPass(Module, method, $"total={total.GetInt32()}, connectors={string.Join(",", connectors)}");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestMappingSearchByConnector()
        {
            const string method = "wiki_securities_mapping_info";
            object request = new { query = KnownTicker, connector = "tinvest" };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                string resultJson = ExtractToolResult(response);

                using (JsonDocument document = JsonDocument.Parse(resultJson))
                {
                    JsonElement root = document.RootElement;

                    if (!root.TryGetProperty("results", out JsonElement results))
                    {
                        _context.RecordFail(Module, method, "results array is missing");
                        return;
                    }

                    foreach (JsonElement item in results.EnumerateArray())
                    {
                        string connectorShort = GetStringProperty(item, "connector_short");

                        if (!string.Equals(connectorShort, "tinvest", StringComparison.OrdinalIgnoreCase))
                        {
                            _context.RecordFail(Module, method, $"unexpected connector '{connectorShort}'");
                            return;
                        }
                    }

                    _context.RecordPass(Module, method, $"count={results.GetArrayLength()}");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestMappingSearchExact()
        {
            const string method = "wiki_securities_mapping_info";
            object request = new { query = KnownTicker, exact = true };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                string resultJson = ExtractToolResult(response);

                using (JsonDocument document = JsonDocument.Parse(resultJson))
                {
                    JsonElement root = document.RootElement;

                    if (!root.TryGetProperty("results", out JsonElement results)
                        || results.GetArrayLength() == 0)
                    {
                        _context.RecordFail(Module, method, "results array is empty or missing");
                        return;
                    }

                    JsonElement first = results[0];
                    JsonElement security = first.GetProperty("security");
                    string name = GetStringProperty(security, "name");

                    if (!string.Equals(name, KnownTicker, StringComparison.OrdinalIgnoreCase))
                    {
                        _context.RecordFail(Module, method, $"first result is not exact match: '{name}'");
                        return;
                    }

                    _context.RecordPass(Module, method, $"exact first={name}");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestMappingSearchLimit()
        {
            const string method = "wiki_securities_mapping_info";
            object request = new { query = KnownTicker, limit = 2 };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                string resultJson = ExtractToolResult(response);

                using (JsonDocument document = JsonDocument.Parse(resultJson))
                {
                    JsonElement root = document.RootElement;

                    if (!root.TryGetProperty("results", out JsonElement results))
                    {
                        _context.RecordFail(Module, method, "results array is missing");
                        return;
                    }

                    if (results.GetArrayLength() > 2)
                    {
                        _context.RecordFail(Module, method, $"results exceed limit: {results.GetArrayLength()}");
                        return;
                    }

                    if (!root.TryGetProperty("total", out JsonElement total)
                        || total.ValueKind != JsonValueKind.Number
                        || total.GetInt32() < results.GetArrayLength())
                    {
                        _context.RecordFail(Module, method, "total is missing or less than results count");
                        return;
                    }

                    _context.RecordPass(Module, method, $"returned={results.GetArrayLength()}, total={total.GetInt32()}");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestMappingSearchNotFound()
        {
            const string method = "wiki_securities_mapping_info";
            object request = new { query = UnknownTicker };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                using (JsonDocument document = JsonDocument.Parse(response))
                {
                    JsonElement root = document.RootElement;

                    if (!root.TryGetProperty("IsError", out JsonElement isError)
                        || isError.ValueKind != JsonValueKind.True)
                    {
                        _context.RecordFail(Module, method, "expected IsError=true for unknown ticker");
                        return;
                    }

                    _context.RecordPass(Module, method, "unknown ticker returned error");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestMappingSearchEmptyQuery()
        {
            const string method = "wiki_securities_mapping_info";
            object request = new { query = "" };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                using (JsonDocument document = JsonDocument.Parse(response))
                {
                    JsonElement root = document.RootElement;

                    if (!root.TryGetProperty("IsError", out JsonElement isError)
                        || isError.ValueKind != JsonValueKind.True)
                    {
                        _context.RecordFail(Module, method, "expected IsError=true for empty query");
                        return;
                    }

                    _context.RecordPass(Module, method, "empty query returned error");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private string ExtractToolResult(string response)
        {
            using (JsonDocument document = JsonDocument.Parse(response))
            {
                JsonElement root = document.RootElement;

                if (root.TryGetProperty("Content", out JsonElement content)
                    && content.GetArrayLength() > 0)
                {
                    JsonElement first = content[0];

                    if (first.TryGetProperty("Text", out JsonElement text)
                        && text.ValueKind == JsonValueKind.String)
                    {
                        return text.GetString() ?? string.Empty;
                    }
                }

                return response;
            }
        }

        private string GetStringProperty(JsonElement element, string propertyName)
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
