/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Collections.Generic;
using System.Text.Json;

namespace OsEngine.IndicatorMarketDepth.TestStand.Tests
{
    public class IndicatorModule : IndicatorTestsBase
    {
        private const string BotStrategy = "MarketDepthIndicatorTestBot";

        private readonly string _indicatorType;

        public IndicatorModule(TestContext context, string indicatorType) : base(context, indicatorType)
        {
            _indicatorType = indicatorType;
        }

        public void Run()
        {
            _context.PrintModuleHeader(Module);

            if (WaitForTesterReady(60) == false)
            {
                RecordFail("TesterReady", "тестер не поднялся");
                return;
            }

            ResetTesterDefaults();
            DeleteAllRobots();

            string setName = GetDataSetName(_indicatorType);
            string dataType = GetTesterDataType(_indicatorType);

            string setResponse = QuietCall("tester_data_set_config", new { set_name = setName });

            if (IsError(setResponse))
            {
                RecordFail("DataSet", GetErrorText(setResponse));
                return;
            }

            string typeResponse = QuietCall("tester_data_set_config", new { type_tester_data = dataType });

            if (IsError(typeResponse))
            {
                RecordFail("DataType", GetErrorText(typeResponse));
                return;
            }

            if (WaitForSecurities(30) == false)
            {
                RecordFail("Securities", "бумаги сета не загрузились");
                return;
            }

            string security = GetFirstSecurityName();

            if (string.IsNullOrEmpty(security))
            {
                RecordFail("Security", "не удалось получить имя бумаги");
                return;
            }

            string botId = CreateBot(security);

            if (string.IsNullOrEmpty(botId))
            {
                RecordFail("BotCreate", "робот не создался");
                return;
            }

            string tabName = GetSimpleTabName(botId);

            if (string.IsNullOrEmpty(tabName))
            {
                RecordFail("BotTab", "не удалось получить tab_name");
                DeleteBot(botId);
                return;
            }

            if (ConfigureSimple(botId, tabName, security) == false)
            {
                RecordFail("BotConfig", "не удалось настроить таб");
                DeleteBot(botId);
                return;
            }

            WaitForReconnect();

            if (SetIndicatorType(botId) == false)
            {
                RecordFail("SetIndicatorType", "не удалось задать тип индикатора");
                DeleteBot(botId);
                return;
            }

            if (Deploy(botId) == false)
            {
                RecordFail("Deploy", "не удалось развернуть индикатор");
                DeleteBot(botId);
                return;
            }

            if (StartTestAndWait(900) == false)
            {
                RecordFail("TestRun", "прогон тестера не завершился");
                DeleteBot(botId);
                return;
            }

            string report = WaitForReport(botId, 120);

            if (string.IsNullOrEmpty(report))
            {
                RecordFail("Report", "не удалось получить отчёт робота");
                DeleteBot(botId);
                return;
            }

            ParseReport(report);

            DeleteBot(botId);
        }

        private string CreateBot(string security)
        {
            string desiredName = "MDITest_" + _indicatorType;
            string response = ToolsCall("bot_create", new { strategy_name = BotStrategy, name = desiredName });

            if (!TryParseJson(response, out JsonElement root))
            {
                return string.Empty;
            }

            return root.TryGetProperty("name", out JsonElement nameElement)
                ? nameElement.GetString() ?? string.Empty
                : string.Empty;
        }

        private string GetSimpleTabName(string botId)
        {
            string response = QuietCall("bot_get_sources", new { bot_id = botId });

            if (!TryParseJson(response, out JsonElement root)
                || !root.TryGetProperty("sources", out JsonElement sources)
                || sources.ValueKind != JsonValueKind.Array)
            {
                return string.Empty;
            }

            foreach (JsonElement source in sources.EnumerateArray())
            {
                string type = source.TryGetProperty("type", out JsonElement typeElement)
                    ? typeElement.GetString() ?? string.Empty
                    : string.Empty;

                if (type == "Simple")
                {
                    return source.TryGetProperty("name", out JsonElement nameElement)
                        ? nameElement.GetString() ?? string.Empty
                        : string.Empty;
                }
            }

            return string.Empty;
        }

        private bool ConfigureSimple(string botId, string tabName, string security)
        {
            string response = ToolsCall("bot_set_config_tab_simple", new
            {
                bot_id = botId,
                tab_name = tabName,
                server_type = "Tester",
                server_name = "Tester",
                portfolio_name = "GodMode",
                emulator_is_on = true,
                candle_market_data_type = "MarketDepth",
                security_name = security,
                time_frame = "Min1"
            });

            return !IsError(response);
        }

        private bool SetIndicatorType(string botId)
        {
            string response = ToolsCall("bot_set_params", new
            {
                bot_id = botId,
                parameters = new Dictionary<string, object>
                {
                    { "IndicatorType", _indicatorType },
                    { "MinDataDays", _context.MinDataDays }
                }
            });

            return !IsError(response);
        }

        private bool Deploy(string botId)
        {
            string response = ToolsCall("bot_click_param_button", new
            {
                bot_id = botId,
                param_name = "DeployIndicator"
            });

            return !IsError(response);
        }

        private string WaitForReport(string botId, int timeoutSeconds)
        {
            string waitResponse = QuietCall("bot_wait_message", new
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

        private void ParseReport(string reportJson)
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
                    int skipped = 0;

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
                            skipped++;
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

        private void DeleteBot(string botId)
        {
            QuietCall("bot_delete", new { bot_id = botId });
        }

        private string GetDataSetName(string indicatorType)
        {
            if (IsTradeIndicator(indicatorType))
            {
                return _context.DataSetName + "Ticks";
            }

            return _context.DataSetName;
        }

        private string GetTesterDataType(string indicatorType)
        {
            if (IsTradeIndicator(indicatorType))
            {
                return "TickAllCandleState";
            }

            return "MarketDepthAllCandleState";
        }

        private bool IsTradeIndicator(string indicatorType)
        {
            return indicatorType == "Delta"
                || indicatorType == "SpeedOfTape"
                || indicatorType == "Rvol"
                || indicatorType == "TradeSizeAnalytics";
        }
    }
}
