/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace OsEngine.McpApi.TestStand.Tests
{
    /// <summary>
    /// Общие тесты режима риск-паритета (HRP): открытие режима, статус,
    /// список ботов (set/add/get/clear), общие настройки (период + диапазон дат).
    /// Открывает режим через terminal_open_mode и оставляет все 50 ботов для CorrelationClustering/HierarchicalRiskParity.
    /// </summary>
    public class CapitalAllocationCommonTests
    {
        private const string Module = "CAPITAL_ALLOCATION";

        private static readonly string TestDataDir =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData");

        private readonly TestContext _context;

        public CapitalAllocationCommonTests(TestContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void RunAll()
        {
            _context.PrintModuleHeader(Module);

            TestStatusWithoutMode();
            TestOpenMode();

            if (!WaitForModeOpen())
            {
                return;
            }

            TestGetStatus();
            TestBotsSet();
            TestBotsAdd();
            TestBotsGet();
            TestBotsClear();
            TestBotsSetAll();
            TestSettings();
        }

        private static string BotPath(string name)
        {
            return Path.Combine(TestDataDir, name);
        }

        private void TestStatusWithoutMode()
        {
            const string method = "allocation_get_status";

            ExpectToolError(method, new { }, "Capital allocation mode is not open");
        }

        private void TestOpenMode()
        {
            const string method = "terminal_open_mode";
            object request = new { mode = "allocation" };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!response.Contains("Opening mode allocation"))
                {
                    _context.RecordFail(Module, method, "response does not contain 'Opening mode allocation'");
                    return;
                }

                _context.RecordPass(Module, method, "operation accepted");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private bool WaitForModeOpen()
        {
            const string method = "allocation_get_status";
            DateTime deadline = DateTime.Now.AddSeconds(30);

            while (DateTime.Now < deadline)
            {
                try
                {
                    string response = _context.Client.ToolsCall(method, new { });

                    using (var document = JsonDocument.Parse(response))
                    {
                        JsonElement root = document.RootElement;

                        if (root.TryGetProperty("IsError", out JsonElement isError) && !isError.GetBoolean()
                            && root.TryGetProperty("Content", out JsonElement content) && content.GetArrayLength() > 0)
                        {
                            string text = content[0].GetProperty("Text").GetString() ?? string.Empty;

                            using (var innerDocument = JsonDocument.Parse(text))
                            {
                                if (innerDocument.RootElement.TryGetProperty("is_open", out JsonElement isOpen)
                                    && isOpen.ValueKind == JsonValueKind.True)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // ignore and retry
                }

                Thread.Sleep(500);
            }

            _context.RecordFail(Module, method, "capital allocation mode is not available after open_mode");
            return false;
        }

        private void TestGetStatus()
        {
            const string method = "allocation_get_status";
            object request = new { };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return;
                }

                if (!config.TryGetProperty("is_open", out JsonElement isOpen)
                    || isOpen.ValueKind != JsonValueKind.True)
                {
                    _context.RecordFail(Module, method, "is_open is not true");
                    return;
                }

                if (!config.TryGetProperty("bots_count", out JsonElement botsCount)
                    || botsCount.GetInt32() < 0)
                {
                    _context.RecordFail(Module, method, "bots_count is invalid");
                    return;
                }

                if (!config.TryGetProperty("period", out JsonElement period)
                    || period.GetString() != "week")
                {
                    _context.RecordFail(Module, method, "period is not week");
                    return;
                }

                if (!config.TryGetProperty("algorithm", out JsonElement algorithm)
                    || algorithm.GetString() != "correlationclustering")
                {
                    _context.RecordFail(Module, method, "algorithm is not correlationclustering");
                    return;
                }

                _context.RecordPass(Module, method, "status received");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestBotsSet()
        {
            const string method = "allocation_bots_set";

            object request = new
            {
                files = new[] { BotPath("1_Deals.txt"), BotPath("2_Deals.txt"), BotPath("3_Deals.txt") }
            };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return;
                }

                if (!config.TryGetProperty("bots_count", out JsonElement botsCount)
                    || botsCount.GetInt32() != 3)
                {
                    _context.RecordFail(Module, method, $"bots_count is not 3 (got {GetBotsCountSafe(config)})");
                    return;
                }

                _context.RecordPass(Module, method, "bots_count=3");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestBotsAdd()
        {
            const string method = "allocation_bots_add";

            object request = new
            {
                files = new[] { BotPath("4_Deals.txt") }
            };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return;
                }

                if (!config.TryGetProperty("bots_count", out JsonElement botsCount)
                    || botsCount.GetInt32() != 4)
                {
                    _context.RecordFail(Module, method, $"bots_count is not 4 (got {GetBotsCountSafe(config)})");
                    return;
                }

                _context.RecordPass(Module, method, "bots_count=4 (append)");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestBotsGet()
        {
            const string method = "allocation_bots_get";
            object request = new { };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return;
                }

                if (!config.TryGetProperty("bots", out JsonElement bots)
                    || bots.ValueKind != JsonValueKind.Array)
                {
                    _context.RecordFail(Module, method, "bots is not an array");
                    return;
                }

                if (bots.GetArrayLength() != 4)
                {
                    _context.RecordFail(Module, method, $"bots array length is not 4 (got {bots.GetArrayLength()})");
                    return;
                }

                foreach (JsonElement bot in bots.EnumerateArray())
                {
                    if (!bot.TryGetProperty("bot_name", out JsonElement botName)
                        || string.IsNullOrWhiteSpace(botName.GetString()))
                    {
                        _context.RecordFail(Module, method, "bot_name is missing or empty");
                        return;
                    }

                    if (!bot.TryGetProperty("path", out JsonElement path)
                        || string.IsNullOrWhiteSpace(path.GetString()))
                    {
                        _context.RecordFail(Module, method, "path is missing or empty");
                        return;
                    }
                }

                _context.RecordPass(Module, method, $"bots_count={bots.GetArrayLength()}");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestBotsClear()
        {
            const string method = "allocation_bots_clear";
            object request = new { };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return;
                }

                if (!config.TryGetProperty("bots_count", out JsonElement botsCount)
                    || botsCount.GetInt32() != 0)
                {
                    _context.RecordFail(Module, method, "bots_count is not 0");
                    return;
                }

                _context.RecordPass(Module, method, "bots cleared");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestBotsSetAll()
        {
            const string method = "allocation_bots_set";

            string[] files = CapitalAllocationTestData.AllFilePaths();
            int expected = CapitalAllocationTestData.BotCount();

            object request = new { files = files };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return;
                }

                if (!config.TryGetProperty("bots_count", out JsonElement botsCount)
                    || botsCount.GetInt32() != expected)
                {
                    _context.RecordFail(Module, method, $"bots_count is not {expected} (got {GetBotsCountSafe(config)})");
                    return;
                }

                _context.RecordPass(Module, method, $"all {expected} bots set for clustering/hrp stages");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestSettings()
        {
            TestSettingsGet();
            TestSettingsSetMonth();
            TestSettingsRestoreWeek();
        }

        private void TestSettingsGet()
        {
            const string method = "allocation_settings_get";
            object request = new { };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return;
                }

                if (!config.TryGetProperty("period", out JsonElement period)
                    || period.GetString() != "week")
                {
                    _context.RecordFail(Module, method, "period is not week");
                    return;
                }

                _context.RecordPass(Module, method, "period=week");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestSettingsSetMonth()
        {
            const string method = "allocation_settings_set";

            object request = new
            {
                period = "month",
                date_from = "2019-01-01T00:00:00",
                date_to = "2021-01-01T00:00:00"
            };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return;
                }

                if (!config.TryGetProperty("period", out JsonElement period)
                    || period.GetString() != "month")
                {
                    _context.RecordFail(Module, method, "period is not month");
                    return;
                }

                if (!config.TryGetProperty("date_from", out JsonElement dateFrom)
                    || string.IsNullOrEmpty(dateFrom.GetString())
                    || !dateFrom.GetString().StartsWith("2019-01-01"))
                {
                    _context.RecordFail(Module, method, "date_from mismatch");
                    return;
                }

                if (!config.TryGetProperty("date_to", out JsonElement dateTo)
                    || string.IsNullOrEmpty(dateTo.GetString())
                    || !dateTo.GetString().StartsWith("2021-01-01"))
                {
                    _context.RecordFail(Module, method, "date_to mismatch");
                    return;
                }

                _context.RecordPass(Module, method, "period=month, dates set");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestSettingsRestoreWeek()
        {
            const string method = "allocation_settings_set";

            object request = new
            {
                period = "week"
            };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return;
                }

                if (!config.TryGetProperty("period", out JsonElement period)
                    || period.GetString() != "week")
                {
                    _context.RecordFail(Module, method, "period is not week");
                    return;
                }

                _context.RecordPass(Module, method, "period restored to week");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void ExpectToolError(string method, object request, string expectedText)
        {
            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                using (var document = JsonDocument.Parse(response))
                {
                    JsonElement root = document.RootElement;

                    if (!root.TryGetProperty("IsError", out JsonElement isError)
                        || isError.ValueKind != JsonValueKind.True)
                    {
                        _context.RecordFail(Module, method, "expected IsError=true");
                        return;
                    }

                    string text = string.Empty;

                    if (root.TryGetProperty("Content", out JsonElement content)
                        && content.GetArrayLength() > 0
                        && content[0].TryGetProperty("Text", out JsonElement textElement))
                    {
                        text = textElement.GetString() ?? string.Empty;
                    }

                    if (text.IndexOf(expectedText, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        _context.RecordFail(Module, method, $"error text does not contain '{expectedText}': {text}");
                        return;
                    }

                    _context.RecordPass(Module, method, $"error as expected: {expectedText}");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private bool TryParseConfig(string response, string method, out JsonElement config)
        {
            config = default;

            try
            {
                using (var document = JsonDocument.Parse(response))
                {
                    JsonElement result = document.RootElement;

                    if (!result.TryGetProperty("IsError", out JsonElement isError) || isError.GetBoolean())
                    {
                        _context.RecordFail(Module, method, "IsError is true");
                        return false;
                    }

                    if (!result.TryGetProperty("Content", out JsonElement content) || content.GetArrayLength() == 0)
                    {
                        _context.RecordFail(Module, method, "Content is empty");
                        return false;
                    }

                    string text = content[0].GetProperty("Text").GetString() ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(text))
                    {
                        _context.RecordFail(Module, method, "Content text is empty");
                        return false;
                    }

                    using (var innerDocument = JsonDocument.Parse(text))
                    {
                        config = innerDocument.RootElement.Clone();
                        return true;
                    }
                }
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, method, error.Message);
                return false;
            }
        }

        private static int GetBotsCountSafe(JsonElement config)
        {
            if (config.TryGetProperty("bots_count", out JsonElement botsCount)
                && botsCount.ValueKind == JsonValueKind.Number)
            {
                return botsCount.GetInt32();
            }

            return -1;
        }
    }
}
