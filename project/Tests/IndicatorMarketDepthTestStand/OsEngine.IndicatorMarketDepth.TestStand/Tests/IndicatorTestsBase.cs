/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Collections.Generic;
using System.Text.Json;
using OsEngine.IndicatorMarketDepth.TestStand;

namespace OsEngine.IndicatorMarketDepth.TestStand.Tests
{
    public abstract class IndicatorTestsBase
    {
        protected readonly TestContext _context;

        protected readonly string Module;

        protected IndicatorTestsBase(TestContext context, string module)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            Module = module;
        }

        #region MCP calls

        protected string ToolsCall(string method, object arguments)
        {
            _context.PrintRequest(Module, method, arguments);
            string response = _context.Client.ToolsCall(method, arguments);
            _context.PrintResponse(response);
            return response;
        }

        protected string QuietCall(string method, object arguments)
        {
            return _context.Client.ToolsCall(method, arguments);
        }

        #endregion

        #region Reporting

        protected void RecordPass(string method, string message)
        {
            _context.RecordPass(Module, method, message);
        }

        protected void RecordFail(string method, string message)
        {
            _context.RecordFail(Module, method, message);
        }

        #endregion

        #region Response parsing

        protected static bool TryGetInnerText(string response, out string text)
        {
            text = string.Empty;

            if (string.IsNullOrWhiteSpace(response))
            {
                return false;
            }

            try
            {
                using (JsonDocument doc = JsonDocument.Parse(response))
                {
                    JsonElement root = doc.RootElement;

                    if (root.TryGetProperty("IsError", out JsonElement isError)
                        && isError.ValueKind == JsonValueKind.True)
                    {
                        return false;
                    }

                    if (root.TryGetProperty("Content", out JsonElement content) && content.GetArrayLength() > 0)
                    {
                        text = content[0].GetProperty("Text").GetString() ?? string.Empty;
                        return string.IsNullOrEmpty(text) == false;
                    }
                }
            }
            catch
            {
                // ignore
            }

            return false;
        }

        protected static bool TryParseJson(string response, out JsonElement root)
        {
            root = default;

            if (!TryGetInnerText(response, out string text))
            {
                return false;
            }

            try
            {
                using (JsonDocument doc = JsonDocument.Parse(text))
                {
                    root = doc.RootElement.Clone();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        protected static bool IsError(string response)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(response))
                {
                    return doc.RootElement.TryGetProperty("IsError", out JsonElement e)
                        && e.ValueKind == JsonValueKind.True;
                }
            }
            catch
            {
                return true;
            }
        }

        protected static string GetErrorText(string response)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(response))
                {
                    if (doc.RootElement.TryGetProperty("Content", out JsonElement content) && content.GetArrayLength() > 0)
                    {
                        return content[0].GetProperty("Text").GetString() ?? string.Empty;
                    }
                }
            }
            catch
            {
                // ignore
            }

            return string.Empty;
        }

        #endregion

        #region Tester helpers

        protected bool WaitForTesterReady(int timeoutSeconds)
        {
            DateTime deadline = DateTime.Now.AddSeconds(timeoutSeconds);

            while (DateTime.Now < deadline)
            {
                string response = QuietCall("tester_data_get_config", new { });

                if (!IsError(response))
                {
                    return true;
                }

                System.Threading.Thread.Sleep(500);
            }

            return false;
        }

        protected void ResetTesterDefaults()
        {
            QuietCall("tester_portfolio_set_config", new
            {
                start_portfolio = 1000000m,
                portfolio_calculation_enabled = false
            });

            QuietCall("tester_execution_set_config", new
            {
                slippage_to_simple_order = 0,
                slippage_to_stop_order = 0,
                order_execution_type = "Touch",
                non_trade_periods = new object[0]
            });
        }

        protected void DeleteAllRobots()
        {
            for (int pass = 0; pass < 20; pass++)
            {
                string list = QuietCall("bot_get_list", new { });

                if (!TryParseJson(list, out JsonElement root)
                    || !root.TryGetProperty("bots", out JsonElement bots)
                    || bots.ValueKind != JsonValueKind.Array)
                {
                    break;
                }

                if (bots.GetArrayLength() == 0)
                {
                    break;
                }

                foreach (JsonElement bot in bots.EnumerateArray())
                {
                    string name = bot.TryGetProperty("name", out JsonElement nameElement)
                        ? nameElement.GetString() ?? string.Empty
                        : string.Empty;

                    if (string.IsNullOrEmpty(name))
                    {
                        continue;
                    }

                    QuietCall("bot_delete", new { bot_id = name });
                }
            }
        }

        protected void WaitForReconnect()
        {
            System.Threading.Thread.Sleep(15000);
        }

        protected bool WaitForSecurities(int timeoutSeconds)
        {
            DateTime deadline = DateTime.Now.AddSeconds(timeoutSeconds);

            while (DateTime.Now < deadline)
            {
                string response = QuietCall("tester_get_securities", new { });

                if (TryParseJson(response, out JsonElement root)
                    && root.TryGetProperty("count", out JsonElement countElement)
                    && countElement.ValueKind == JsonValueKind.Number
                    && countElement.GetInt32() > 0)
                {
                    return true;
                }

                System.Threading.Thread.Sleep(1000);
            }

            return false;
        }

        protected string GetFirstSecurityName()
        {
            WaitForSecurities(30);

            string response = QuietCall("tester_get_securities", new { });

            if (!TryParseJson(response, out JsonElement root)
                || !root.TryGetProperty("securities", out JsonElement list)
                || list.ValueKind != JsonValueKind.Array)
            {
                return string.Empty;
            }

            foreach (JsonElement item in list.EnumerateArray())
            {
                string name = item.TryGetProperty("name", out JsonElement nameElement)
                    ? nameElement.GetString() ?? string.Empty
                    : string.Empty;

                if (!string.IsNullOrEmpty(name))
                {
                    return name;
                }
            }

            return string.Empty;
        }

        protected bool StartTestAndWait(int timeoutSeconds)
        {
            DateTime startDeadline = DateTime.Now.AddSeconds(60);
            bool playing = false;

            while (DateTime.Now < startDeadline && playing == false)
            {
                string response = QuietCall("tester_start", new { fast_forward = true });

                if (TryParseJson(response, out JsonElement status))
                {
                    string regime = status.TryGetProperty("regime", out JsonElement regimeElement)
                        ? regimeElement.GetString() ?? string.Empty
                        : string.Empty;

                    if (regime == "Play")
                    {
                        playing = true;
                    }
                }

                if (playing == false)
                {
                    System.Threading.Thread.Sleep(2000);
                }
            }

            if (playing == false)
            {
                return false;
            }

            DateTime deadline = DateTime.Now.AddSeconds(timeoutSeconds);

            while (DateTime.Now < deadline)
            {
                string response = QuietCall("tester_get_status", new { });

                if (TryParseJson(response, out JsonElement status))
                {
                    string regime = status.TryGetProperty("regime", out JsonElement regimeElement)
                        ? regimeElement.GetString() ?? string.Empty
                        : string.Empty;

                    bool fastForward = status.TryGetProperty("fast_forward", out JsonElement fastForwardElement)
                        && fastForwardElement.ValueKind == JsonValueKind.True;

                    double progress = status.TryGetProperty("progress_percent", out JsonElement progressElement)
                        && progressElement.ValueKind == JsonValueKind.Number
                        ? progressElement.GetDouble()
                        : 0;

                    if (regime == "NotActive" || progress >= 100)
                    {
                        return true;
                    }

                    if (regime == "Play" && fastForward == false)
                    {
                        QuietCall("tester_fast_forward", new { });
                    }
                }

                System.Threading.Thread.Sleep(1000);
            }

            return false;
        }

        #endregion

        #region Report helpers

        protected string WaitForReportJson(string botId, int timeoutSeconds)
        {
            QuietCall("bot_wait_message", new
            {
                bot_id = botId,
                marker = "MDI_TEST_DONE",
                timeout_seconds = timeoutSeconds
            });

            string paramsResponse = QuietCall("bot_get_params", new { bot_id = botId });

            if (!TryParseJson(paramsResponse, out JsonElement root)
                || !root.TryGetProperty("parameters", out JsonElement parameters)
                || parameters.ValueKind != JsonValueKind.Array)
            {
                return string.Empty;
            }

            foreach (JsonElement parameter in parameters.EnumerateArray())
            {
                string name = parameter.TryGetProperty("name", out JsonElement nameElement)
                    ? nameElement.GetString() ?? string.Empty
                    : string.Empty;

                if (name != "ReportJson")
                {
                    continue;
                }

                string value = parameter.TryGetProperty("value", out JsonElement valueElement)
                    ? valueElement.GetString() ?? string.Empty
                    : string.Empty;

                return string.IsNullOrWhiteSpace(value) ? string.Empty : value;
            }

            return string.Empty;
        }

        protected void ParseReport(string reportJson)
        {
            try
            {
                using (JsonDocument document = JsonDocument.Parse(reportJson))
                {
                    JsonElement root = document.RootElement;

                    if (!root.TryGetProperty("tests", out JsonElement tests)
                        || tests.ValueKind != JsonValueKind.Array)
                    {
                        RecordFail("ReportFormat", "в отчёте нет массива tests");
                        return;
                    }

                    int passed = 0;
                    int failed = 0;

                    foreach (JsonElement test in tests.EnumerateArray())
                    {
                        string name = test.TryGetProperty("name", out JsonElement nameElement)
                            ? nameElement.GetString() ?? "unknown"
                            : "unknown";

                        string status = test.TryGetProperty("status", out JsonElement statusElement)
                            ? statusElement.GetString() ?? "FAIL"
                            : "FAIL";

                        string details = test.TryGetProperty("details", out JsonElement detailsElement)
                            ? detailsElement.GetString() ?? string.Empty
                            : string.Empty;

                        if (status == "PASS")
                        {
                            passed++;
                            RecordPass(name, details);
                        }
                        else if (status == "SKIP")
                        {
                            RecordPass(name, "SKIP: " + details);
                        }
                        else
                        {
                            failed++;
                            RecordFail(name, details);
                        }
                    }

                    if (failed == 0 && passed == 0)
                    {
                        RecordFail("ReportContent", "отчёт не содержит ни одного теста");
                    }
                }
            }
            catch (Exception error)
            {
                RecordFail("ReportParse", error.Message);
            }
        }

        #endregion
    }
}
