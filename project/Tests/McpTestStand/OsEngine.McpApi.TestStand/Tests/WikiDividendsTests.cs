/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Linq;
using System.Text.Json;

namespace OsEngine.McpApi.TestStand.Tests
{
    /// <summary>
    /// Tests for dividend wiki reference methods.
    /// </summary>
    public class WikiDividendsTests
    {
        private const string Module = "WIKI_DIVIDENDS";
        private const string KnownTicker = "SBER";
        private const string KnownRegistryDate = "18.07.2025";
        private const string UnknownTicker = "ThisTickerDoesNotExist12345";

        private readonly TestContext _context;

        public WikiDividendsTests(TestContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void RunAll()
        {
            _context.PrintModuleHeader(Module);

            TestToolsListContainsWikiDividendsMethods();
            TestGetHistoryKnownTicker();
            TestGetHistoryWithDate();
            TestGetFutureKnownTicker();
            TestGetFutureWithDate();
            TestGetNearestKnownTicker();
            TestSearchByDateKnown();
            TestSearchByDateNotFound();
            TestGetHistoryUnknownTicker();
            TestGetHistoryEmptyTicker();
            TestSearchByDateEmptyDate();
        }

        private void TestToolsListContainsWikiDividendsMethods()
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
                        "wiki_dividends_get_history",
                        "wiki_dividends_get_future",
                        "wiki_dividends_get_nearest",
                        "wiki_dividends_search_by_date"
                    };

                    foreach (string expected in expectedTools)
                    {
                        if (!toolNames.Contains(expected))
                        {
                            _context.RecordFail(Module, method, $"{expected} tool is missing");
                            return;
                        }
                    }

                    _context.RecordPass(Module, method, "all wiki dividends tools registered");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestGetHistoryKnownTicker()
        {
            const string method = "wiki_dividends_get_history";
            object request = new { ticker = KnownTicker };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                string resultJson = ExtractToolResult(response);

                using (JsonDocument document = JsonDocument.Parse(resultJson))
                {
                    JsonElement root = document.RootElement;

                    if (!root.TryGetProperty("ticker", out JsonElement ticker)
                        || !string.Equals(ticker.GetString(), KnownTicker, StringComparison.OrdinalIgnoreCase))
                    {
                        _context.RecordFail(Module, method, "ticker missing or mismatch");
                        return;
                    }

                    if (!root.TryGetProperty("historical", out JsonElement historical)
                        || historical.GetArrayLength() == 0)
                    {
                        _context.RecordFail(Module, method, "historical dividends missing or empty");
                        return;
                    }

                    if (!root.TryGetProperty("count", out JsonElement count)
                        || count.ValueKind != JsonValueKind.Number
                        || count.GetInt32() != historical.GetArrayLength())
                    {
                        _context.RecordFail(Module, method, "count missing or mismatch");
                        return;
                    }

                    _context.RecordPass(Module, method, $"count={historical.GetArrayLength()}");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestGetHistoryWithDate()
        {
            const string method = "wiki_dividends_get_history";
            object request = new { ticker = KnownTicker, date = "01.01.2020" };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                string resultJson = ExtractToolResult(response);

                using (JsonDocument document = JsonDocument.Parse(resultJson))
                {
                    JsonElement root = document.RootElement;

                    if (!root.TryGetProperty("date", out JsonElement dateElement)
                        || !string.Equals(dateElement.GetString(), "01.01.2020"))
                    {
                        _context.RecordFail(Module, method, "date missing or mismatch");
                        return;
                    }

                    if (!root.TryGetProperty("historical", out JsonElement historical)
                        || historical.GetArrayLength() == 0)
                    {
                        _context.RecordFail(Module, method, "historical dividends missing or empty");
                        return;
                    }

                    foreach (JsonElement record in historical.EnumerateArray())
                    {
                        string registryCloseDate = GetStringProperty(record, "registry_close_date");
                        if (!TryParseDate(registryCloseDate, out DateTime recordDate)
                            || recordDate > new DateTime(2020, 1, 1))
                        {
                            _context.RecordFail(Module, method, $"record after reference date: {registryCloseDate}");
                            return;
                        }
                    }

                    _context.RecordPass(Module, method, $"count={historical.GetArrayLength()}");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestGetFutureKnownTicker()
        {
            const string method = "wiki_dividends_get_future";
            object request = new { ticker = KnownTicker };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                string resultJson = ExtractToolResult(response);

                using (JsonDocument document = JsonDocument.Parse(resultJson))
                {
                    JsonElement root = document.RootElement;

                    if (!root.TryGetProperty("ticker", out _))
                    {
                        _context.RecordFail(Module, method, "ticker missing");
                        return;
                    }

                    if (!root.TryGetProperty("future", out JsonElement future))
                    {
                        _context.RecordFail(Module, method, "future dividends missing");
                        return;
                    }

                    _context.RecordPass(Module, method, $"count={future.GetArrayLength()}");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestGetFutureWithDate()
        {
            const string method = "wiki_dividends_get_future";
            object request = new { ticker = KnownTicker, date = "01.01.2025" };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                string resultJson = ExtractToolResult(response);

                using (JsonDocument document = JsonDocument.Parse(resultJson))
                {
                    JsonElement root = document.RootElement;

                    if (!root.TryGetProperty("date", out JsonElement dateElement)
                        || !string.Equals(dateElement.GetString(), "01.01.2025"))
                    {
                        _context.RecordFail(Module, method, "date missing or mismatch");
                        return;
                    }

                    if (!root.TryGetProperty("future", out JsonElement future)
                        || future.GetArrayLength() == 0)
                    {
                        _context.RecordFail(Module, method, "future dividends missing or empty");
                        return;
                    }

                    foreach (JsonElement record in future.EnumerateArray())
                    {
                        string registryCloseDate = GetStringProperty(record, "registry_close_date");
                        if (!TryParseDate(registryCloseDate, out DateTime recordDate)
                            || recordDate < new DateTime(2025, 1, 1))
                        {
                            _context.RecordFail(Module, method, $"record before reference date: {registryCloseDate}");
                            return;
                        }
                    }

                    _context.RecordPass(Module, method, $"count={future.GetArrayLength()}");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestGetNearestKnownTicker()
        {
            const string method = "wiki_dividends_get_nearest";
            object request = new { ticker = KnownTicker };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                string resultJson = ExtractToolResult(response);

                using (JsonDocument document = JsonDocument.Parse(resultJson))
                {
                    JsonElement root = document.RootElement;

                    if (!root.TryGetProperty("ticker", out _))
                    {
                        _context.RecordFail(Module, method, "ticker missing");
                        return;
                    }

                    if (!root.TryGetProperty("nearest", out JsonElement nearest)
                        || nearest.ValueKind == JsonValueKind.Null)
                    {
                        _context.RecordFail(Module, method, "nearest dividend missing");
                        return;
                    }

                    if (!nearest.TryGetProperty("registry_close_date", out _))
                    {
                        _context.RecordFail(Module, method, "nearest registry_close_date missing");
                        return;
                    }

                    _context.RecordPass(Module, method, "nearest dividend found");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestSearchByDateKnown()
        {
            const string method = "wiki_dividends_search_by_date";
            object request = new { ticker = KnownTicker, date = KnownRegistryDate };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                string resultJson = ExtractToolResult(response);

                using (JsonDocument document = JsonDocument.Parse(resultJson))
                {
                    JsonElement root = document.RootElement;

                    if (!root.TryGetProperty("date", out _))
                    {
                        _context.RecordFail(Module, method, "date missing");
                        return;
                    }

                    if (!root.TryGetProperty("matches", out JsonElement matches)
                        || matches.GetArrayLength() == 0)
                    {
                        _context.RecordFail(Module, method, "matches missing or empty");
                        return;
                    }

                    _context.RecordPass(Module, method, $"count={matches.GetArrayLength()}");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestSearchByDateNotFound()
        {
            const string method = "wiki_dividends_search_by_date";
            object request = new { ticker = KnownTicker, date = "01.01.1900" };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                string resultJson = ExtractToolResult(response);

                using (JsonDocument document = JsonDocument.Parse(resultJson))
                {
                    JsonElement root = document.RootElement;

                    if (!root.TryGetProperty("matches", out JsonElement matches)
                        || matches.GetArrayLength() != 0)
                    {
                        _context.RecordFail(Module, method, "expected empty matches");
                        return;
                    }

                    _context.RecordPass(Module, method, "empty matches for old date");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestGetHistoryUnknownTicker()
        {
            const string method = "wiki_dividends_get_history";
            object request = new { ticker = UnknownTicker };

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

        private void TestGetHistoryEmptyTicker()
        {
            const string method = "wiki_dividends_get_history";
            object request = new { ticker = "" };

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
                        _context.RecordFail(Module, method, "expected IsError=true for empty ticker");
                        return;
                    }

                    _context.RecordPass(Module, method, "empty ticker returned error");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestSearchByDateEmptyDate()
        {
            const string method = "wiki_dividends_search_by_date";
            object request = new { ticker = KnownTicker, date = "" };

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
                        _context.RecordFail(Module, method, "expected IsError=true for empty date");
                        return;
                    }

                    _context.RecordPass(Module, method, "empty date returned error");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
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

        private bool TryParseDate(string value, out DateTime date)
        {
            date = DateTime.MinValue;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return DateTime.TryParseExact(value.Trim(), "dd.MM.yyyy",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out date);
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
    }
}
